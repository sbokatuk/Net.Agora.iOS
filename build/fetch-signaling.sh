#!/usr/bin/env bash
set -euo pipefail

# Downloads the Agora Signaling (RTM) iOS SDK and stages the frameworks Net.Agora.Signaling.iOS
# needs into src/Net.Agora.Signaling.iOS/lib.
#
# Same mechanics as fetch-video.sh: the zip behind the CocoaPods source URL, downloaded directly
# and verified against build/checksums.txt, no CocoaPods or Ruby required. Signaling is a
# different product from the RTC pair with its own native version line (2.2.x).
#
# Usage:
#   ./build/fetch-signaling.sh            # the version pinned in Directory.Build.props
#   ./build/fetch-signaling.sh 2.2.6      # an explicit version
#
# Check what is actually published before bumping the pin:
#   curl -s https://trunk.cocoapods.org/api/v1/pods/AgoraRtm_iOS | python3 -m json.tool

cd "$(dirname "$0")"

ROOT="$(cd .. && pwd)"
CHECKSUMS="$ROOT/build/checksums.txt"
DEST="$ROOT/src/Net.Agora.Signaling.iOS/lib"

VERSION="${1:-}"
if [ -z "${VERSION}" ]; then
    VERSION=$(sed -n 's:.*<AgoraSignalingIosVersion>\(.*\)</AgoraSignalingIosVersion>.*:\1:p' "$ROOT/Directory.Build.props" | head -1)
fi

if [ -z "${VERSION}" ]; then
    echo "error: could not read AgoraSignalingIosVersion from $ROOT/Directory.Build.props" >&2
    exit 1
fi

# The version is interpolated into a URL and a path, so reject anything exotic up front.
case "$VERSION" in
    *[!A-Za-z0-9._-]*)
        echo "error: invalid version '$VERSION'" >&2
        exit 1
        ;;
esac

expected=$(sed -n "s/^AgoraRtm_iOS-$VERSION[[:space:]]\{1,\}\([0-9a-f]\{64\}\).*/\1/p" "$CHECKSUMS" | head -1)
if [ -z "$expected" ]; then
    echo "error: no SHA-256 recorded for AgoraRtm_iOS-$VERSION in $CHECKSUMS" >&2
    echo "       see the instructions at the top of that file for how to add one" >&2
    exit 1
fi

sha256_of() {
    if command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "$1" | cut -d' ' -f1
    else
        sha256sum "$1" | cut -d' ' -f1
    fi
}

echo "Fetching Agora Signaling iOS SDK $VERSION..."

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

curl -fL -o "$WORK/sdk.zip" "https://download.agora.io/sdk/release/AgoraRtm_iOS-$VERSION.zip"

actual=$(sha256_of "$WORK/sdk.zip")
if [ "$actual" != "$expected" ]; then
    echo "error: checksum mismatch for AgoraRtm_iOS-$VERSION.zip" >&2
    echo "       expected $expected" >&2
    echo "       actual   $actual" >&2
    exit 1
fi

unzip -q "$WORK/sdk.zip" -d "$WORK/extracted"

rm -rf "$DEST"
mkdir -p "$DEST"

# The whole zip: the API surface (AgoraRtmKit) and aosl, its one @rpath dependency. No optional
# feature plugins to exclude on this product.
for framework in AgoraRtmKit aosl; do
    if [ ! -d "$WORK/extracted/$framework.xcframework" ]; then
        echo "error: $framework.xcframework not found in the downloaded SDK" >&2
        exit 1
    fi
    cp -R "$WORK/extracted/$framework.xcframework" "$DEST/"
done

# Agora ships visionOS (xros) slices in this SDK — the only one of the three products that has
# them. They are stripped: the packages target net*-ios only, so the slices would be invisible
# dead weight in every consumer's restore, and the package tests deliberately hold the payload to
# iOS device + simulator slices. The xcframework's Info.plist has to agree with what is on disk
# (the iOS SDK rejects a manifest that advertises a missing slice), so the matching
# AvailableLibraries entries are pruned too.
python3 - "$DEST" <<'PYEOF'
import plistlib, shutil, sys
from pathlib import Path

dest = Path(sys.argv[1])
for manifest in dest.glob("*.xcframework/Info.plist"):
    bundle = manifest.parent
    with open(manifest, "rb") as f:
        plist = plistlib.load(f)

    kept = []
    for library in plist["AvailableLibraries"]:
        identifier = library["LibraryIdentifier"]
        if identifier.startswith("xros"):
            shutil.rmtree(bundle / identifier, ignore_errors=True)
        else:
            kept.append(library)
    plist["AvailableLibraries"] = kept

    with open(manifest, "wb") as f:
        plistlib.dump(plist, f)
    print(f"{bundle.name}: kept {', '.join(l['LibraryIdentifier'] for l in kept)}")
PYEOF

echo "Staged $(ls "$DEST" | wc -l | tr -d ' ') frameworks into $DEST"
echo "No maccatalyst slice is shipped for this SDK, so there is no Net.Agora.Signaling.Mac"
echo "project. See README.md."

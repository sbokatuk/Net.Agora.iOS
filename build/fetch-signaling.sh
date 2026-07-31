#!/usr/bin/env bash
set -euo pipefail

# Downloads the Agora Signaling (RTM) iOS SDK and stages the frameworks Net.Agora.Signaling.iOS
# needs into src/Net.Agora.Signaling.iOS/lib.
#
# Same mechanics as fetch-video.sh: the zip behind the CocoaPods source URL, downloaded directly
# and verified against build/checksums.txt, no CocoaPods or Ruby required. Signaling is a
# different product from the RTC pair with its own native version line (2.2.x).
#
# Two archives, like fetch-chat.sh, and for a related reason. AgoraRtm_iOS does bundle its own
# aosl.framework — but at 1.2.13, where the RTC pods bundle 1.3.5, and the older one does not
# export aosl_ref_magic, which AgoraRtcKit resolves at load. An app that references Signaling and
# either RTC package gets one aosl.framework in its bundle, not two, so whichever package's copy
# the build keeps is the one both products have to run against; when that was this one, the RTC
# engine failed to load. The bundled copy is therefore dropped on the floor and aosl comes from
# AgoraInfra_iOS instead, which carries the RTC pods' 1.3.5 byte for byte. Verify after a bump:
#
#   nm -gU .../aosl.framework/aosl | grep aosl_ref_magic     # must be non-empty
#
# The two archives are versioned independently, so both versions are pinned in
# Directory.Build.props and both are checksummed.
#
# Usage:
#   ./build/fetch-signaling.sh            # the versions pinned in Directory.Build.props
#   ./build/fetch-signaling.sh 2.2.6      # an explicit RTM version, pinned Infra version
#   ./build/fetch-signaling.sh 2.2.6 1.3.9  # both explicit
#
# Check what is actually published before bumping either pin:
#   curl -s https://trunk.cocoapods.org/api/v1/pods/AgoraRtm_iOS  | python3 -m json.tool
#   curl -s https://trunk.cocoapods.org/api/v1/pods/AgoraInfra_iOS | python3 -m json.tool

cd "$(dirname "$0")"

ROOT="$(cd .. && pwd)"
CHECKSUMS="$ROOT/build/checksums.txt"
DEST="$ROOT/src/Net.Agora.Signaling.iOS/lib"

read_pin() {
    sed -n "s:.*<$1>\(.*\)</$1>.*:\1:p" "$ROOT/Directory.Build.props" | head -1
}

VERSION="${1:-}"
if [ -z "${VERSION}" ]; then
    VERSION=$(read_pin AgoraSignalingIosVersion)
fi

INFRA_VERSION="${2:-}"
if [ -z "${INFRA_VERSION}" ]; then
    INFRA_VERSION=$(read_pin AgoraSignalingInfraIosVersion)
fi

if [ -z "${VERSION}" ] || [ -z "${INFRA_VERSION}" ]; then
    echo "error: could not read AgoraSignalingIosVersion / AgoraSignalingInfraIosVersion from" >&2
    echo "       $ROOT/Directory.Build.props" >&2
    exit 1
fi

sha256_of() {
    if command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "$1" | cut -d' ' -f1
    else
        sha256sum "$1" | cut -d' ' -f1
    fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# Downloads <artifact>-<version>.zip, verifies it against build/checksums.txt and extracts it into
# a staging directory of its own. Both archives unpack an aosl.xcframework at their top level, so
# one shared directory would have them overwrite each other — and while the extraction order
# happens to give the right binaries, RTM's aosl also carries visionOS slices AgoraInfra's does
# not, which would survive the merge as directories no manifest advertises. A separate directory
# each, and an explicit copy below, says which framework comes from where.
fetch_archive() {
    artifact="$1"
    version="$2"

    # The version is interpolated into a URL and a path, so reject anything exotic up front.
    case "$version" in
        *[!A-Za-z0-9._-]*)
            echo "error: invalid version '$version' for $artifact" >&2
            exit 1
            ;;
    esac

    expected=$(sed -n "s/^$artifact-$version[[:space:]]\{1,\}\([0-9a-f]\{64\}\).*/\1/p" "$CHECKSUMS" | head -1)
    if [ -z "$expected" ]; then
        echo "error: no SHA-256 recorded for $artifact-$version in $CHECKSUMS" >&2
        echo "       see the instructions at the top of that file for how to add one" >&2
        exit 1
    fi

    echo "Fetching $artifact $version..."
    curl -fL -o "$WORK/$artifact.zip" "https://download.agora.io/sdk/release/$artifact-$version.zip"

    actual=$(sha256_of "$WORK/$artifact.zip")
    if [ "$actual" != "$expected" ]; then
        echo "error: checksum mismatch for $artifact-$version.zip" >&2
        echo "       expected $expected" >&2
        echo "       actual   $actual" >&2
        exit 1
    fi

    unzip -q "$WORK/$artifact.zip" -d "$WORK/$artifact"
}

fetch_archive AgoraRtm_iOS "$VERSION"
fetch_archive AgoraInfra_iOS "$INFRA_VERSION"

rm -rf "$DEST"
mkdir -p "$DEST"

# The API surface from the RTM zip, and aosl — its one @rpath dependency — from AgoraInfra rather
# than from the copy the RTM zip also carries. No optional feature plugins to exclude on this
# product. The RTM zip's own aosl.xcframework is simply never staged; see the header.
stage() {
    artifact="$1"
    framework="$2"

    if [ ! -d "$WORK/$artifact/$framework.xcframework" ]; then
        echo "error: $framework.xcframework not found in $artifact" >&2
        exit 1
    fi
    cp -R "$WORK/$artifact/$framework.xcframework" "$DEST/"
}

stage AgoraRtm_iOS  AgoraRtmKit
stage AgoraInfra_iOS aosl


# Agora ships visionOS (xros) slices in AgoraRtmKit — the only framework staged by any of these
# scripts that has them. They are stripped: the packages target net*-ios only, so the slices would be invisible
# dead weight in every consumer's restore, and the package tests deliberately hold the payload to
# iOS device + simulator slices. The xcframework's Info.plist has to agree with what is on disk
# (the iOS SDK rejects a manifest that advertises a missing slice), so the matching
# AvailableLibraries entries are pruned too.
#
# The device slice is also thinned: Agora still builds it as ios-arm64_armv7, and .NET for iOS
# has not supported 32-bit armv7 since .NET 6 — the architecture is unreachable dead weight
# repeated once per packed target framework. lipo removes it from every Mach-O in the slice, the
# slice directory is renamed to plain ios-arm64, and the manifest entry is rewritten to match
# (identifier and SupportedArchitectures both — the SDK cross-checks them against the binary).
python3 - "$DEST" <<'PYEOF'
import plistlib, shutil, subprocess, sys
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
            continue

        architectures = library.get("SupportedArchitectures", [])
        if "armv7" in architectures:
            slice_dir = bundle / identifier
            framework = slice_dir / library["LibraryPath"]
            binary = framework / framework.stem
            subprocess.run(
                ["lipo", binary, "-remove", "armv7", "-output", binary], check=True)

            new_identifier = identifier.replace("_armv7", "").replace("armv7_", "")
            (bundle / identifier).rename(bundle / new_identifier)
            library["LibraryIdentifier"] = new_identifier
            library["SupportedArchitectures"] = [a for a in architectures if a != "armv7"]

        kept.append(library)
    plist["AvailableLibraries"] = kept

    with open(manifest, "wb") as f:
        plistlib.dump(plist, f)
    print(f"{bundle.name}: kept {', '.join(l['LibraryIdentifier'] for l in kept)}")
PYEOF

# The whole reason the second archive is here, checked rather than assumed: a rearranged staging
# order, or an AgoraInfra release that stops shipping aosl, would put AgoraRtm_iOS's own 1.2.x
# copy back and reintroduce exactly the conflict this fetch exists to avoid — silently, in a
# package that looks entirely normal. Run after the pruning above so only the slices that actually
# ship are examined, and one slice at a time because `nm` on a fat or non-Mach-O file exits
# non-zero and `set -o pipefail` would then hide a perfectly good grep match behind it.
for binary in "$DEST"/aosl.xcframework/*/aosl.framework/aosl; do
    symbols=$(nm -gU "$binary" 2>/dev/null || true)
    case "$symbols" in
        *aosl_ref_magic*) ;;
        *)
            echo "error: $binary does not export aosl_ref_magic, so AgoraRtcKit cannot load" >&2
            echo "       against it. That is AgoraRtm_iOS's own bundled 1.2.x aosl, not the one" >&2
            echo "       from AgoraInfra_iOS-$INFRA_VERSION. See the header of this script." >&2
            exit 1
            ;;
    esac
done

echo "Staged $(ls "$DEST" | wc -l | tr -d ' ') frameworks into $DEST"
echo "No maccatalyst slice is shipped for this SDK, so there is no Net.Agora.Signaling.Mac"
echo "project. See README.md."

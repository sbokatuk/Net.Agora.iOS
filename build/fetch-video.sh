#!/usr/bin/env bash
set -euo pipefail

# Downloads the Agora Video (RTC) iOS SDK and stages the frameworks Net.Agora.Video.iOS needs into
# src/Net.Agora.Video.iOS/lib.
#
# Unlike the Android side (which pulls straight from Maven Central via AndroidMavenLibrary), Agora
# does not publish the iOS SDK to Maven-equivalent infrastructure .NET can consume directly — the
# real artifact lives behind the CocoaPods source URL. This script downloads exactly that zip
# (the same one `pod install` would fetch for the "RtcBasic" subspec of AgoraRtcEngine_iOS), without
# requiring CocoaPods or Ruby.
#
# Usage:
#   ./build/fetch-video.sh            # the version pinned in Directory.Build.props
#   ./build/fetch-video.sh 4.6.2      # an explicit version
#
# Note the iOS pod version (AgoraVideoIosVersion) and the Android Maven version (AgoraVideoVersion,
# in the sibling Net.Agora.Android repository) are not always the same release — Agora ships the
# two platforms on independent schedules, and the iOS pod can lag behind Maven Central by a point
# release. Check what is actually published before bumping either pin:
#   curl -s https://trunk.cocoapods.org/api/v1/pods/AgoraRtcEngine_iOS | python3 -m json.tool

cd "$(dirname "$0")"

ROOT="$(cd .. && pwd)"
CHECKSUMS="$ROOT/build/checksums.txt"
DEST="$ROOT/src/Net.Agora.Video.iOS/lib"

VERSION="${1:-}"
if [ -z "${VERSION}" ]; then
    VERSION=$(sed -n 's:.*<AgoraVideoIosVersion>\(.*\)</AgoraVideoIosVersion>.*:\1:p' "$ROOT/Directory.Build.props" | head -1)
fi

if [ -z "${VERSION}" ]; then
    echo "error: could not read AgoraVideoIosVersion from $ROOT/Directory.Build.props" >&2
    exit 1
fi

# The version is interpolated into a URL and a path, so reject anything exotic up front.
case "$VERSION" in
    *[!A-Za-z0-9._-]*)
        echo "error: invalid version '$VERSION'" >&2
        exit 1
        ;;
esac

expected=$(sed -n "s/^$VERSION[[:space:]]\{1,\}\([0-9a-f]\{64\}\).*/\1/p" "$CHECKSUMS" | head -1)
if [ -z "$expected" ]; then
    echo "error: no SHA-256 recorded for $VERSION in $CHECKSUMS" >&2
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

echo "Fetching Agora Video iOS SDK $VERSION..."

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

curl -fL -o "$WORK/sdk.zip" "https://download.agora.io/sdk/release/AgoraRtcEngine_iOS-$VERSION.zip"

actual=$(sha256_of "$WORK/sdk.zip")
if [ "$actual" != "$expected" ]; then
    echo "error: checksum mismatch for AgoraRtcEngine_iOS-$VERSION.zip" >&2
    echo "       expected $expected" >&2
    echo "       actual   $actual" >&2
    exit 1
fi

unzip -q "$WORK/sdk.zip" -d "$WORK/extracted"

rm -rf "$DEST"
mkdir -p "$DEST"

# Only the "RtcBasic" subspec: the API surface (AgoraRtcKit) plus the codec/infra dependencies it
# links against — every @rpath entry `otool -L` reports on AgoraRtcKit's own binary, confirmed
# against both simulator slices. The other ~20 xcframeworks in the zip are optional feature plugins
# (AI noise suppression, face detection, virtual background, ...) that AgoraRtcKit does not itself
# depend on — same reasoning as full-rtc-basic vs. full-sdk on Android, see
# src/Net.Agora.Video.Android/Net.Agora.Video.Android.csproj in the sibling Net.Agora.Android
# repository. video_enc.xcframework, video_dec's sibling, is one such plugin: AgoraRtcKit's binary
# does not reference it.
for framework in AgoraRtcKit aosl Agorafdkaac Agoraffmpeg AgoraSoundTouch video_dec; do
    if [ ! -d "$WORK/extracted/$framework.xcframework" ]; then
        echo "error: $framework.xcframework not found in the downloaded SDK" >&2
        exit 1
    fi
    cp -R "$WORK/extracted/$framework.xcframework" "$DEST/"
done

echo "Staged $(ls "$DEST" | wc -l | tr -d ' ') frameworks into $DEST"
echo "No maccatalyst slice is shipped for this SDK (ios-arm64 and simulator only), so there is no"
echo "Net.Agora.Video.Mac project. See README.md."

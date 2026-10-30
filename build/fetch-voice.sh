#!/usr/bin/env bash
set -euo pipefail

# Downloads the Agora Voice iOS SDK and stages the frameworks Net.Agora.Voice.iOS needs into
# src/Net.Agora.Voice.iOS/lib.
#
# The Voice pod (AgoraAudio_iOS) is the Video pod's engine built without the video pipeline — the
# same AgoraRtcKit framework name and headers, a smaller binary, and no video codec dependencies.
# Same mechanics as fetch-video.sh otherwise: the zip behind the CocoaPods source URL, downloaded
# directly and verified against build/checksums.txt, no CocoaPods or Ruby required.
#
# Usage:
#   ./build/fetch-voice.sh            # the version pinned in Directory.Build.props
#   ./build/fetch-voice.sh 4.6.2      # an explicit version
#
# Note the iOS pod version (AgoraVoiceIosVersion) and the Android Maven version (AgoraVoiceVersion,
# in the sibling Net.Agora.Android repository) are not always the same release — Agora ships the
# two platforms on independent schedules. Check what is actually published before bumping either
# pin:
#   curl -s https://trunk.cocoapods.org/api/v1/pods/AgoraAudio_iOS | python3 -m json.tool

cd "$(dirname "$0")"

ROOT="$(cd .. && pwd)"
CHECKSUMS="$ROOT/build/checksums.txt"
DEST="$ROOT/src/Net.Agora.Voice.iOS/lib"

VERSION="${1:-}"
if [ -z "${VERSION}" ]; then
    VERSION=$(sed -n 's:.*<AgoraVoiceIosVersion>\(.*\)</AgoraVoiceIosVersion>.*:\1:p' "$ROOT/Directory.Build.props" | head -1)
fi

if [ -z "${VERSION}" ]; then
    echo "error: could not read AgoraVoiceIosVersion from $ROOT/Directory.Build.props" >&2
    exit 1
fi

# The version is interpolated into a URL and a path, so reject anything exotic up front.
case "$VERSION" in
    *[!A-Za-z0-9._-]*)
        echo "error: invalid version '$VERSION'" >&2
        exit 1
        ;;
esac

expected=$(sed -n "s/^AgoraAudio_iOS-$VERSION[[:space:]]\{1,\}\([0-9a-f]\{64\}\).*/\1/p" "$CHECKSUMS" | head -1)
if [ -z "$expected" ]; then
    echo "error: no SHA-256 recorded for AgoraAudio_iOS-$VERSION in $CHECKSUMS" >&2
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

echo "Fetching Agora Voice iOS SDK $VERSION..."

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

curl -fL -o "$WORK/sdk.zip" "https://download.agora.io/sdk/release/AgoraAudio_iOS-$VERSION.zip"

actual=$(sha256_of "$WORK/sdk.zip")
if [ "$actual" != "$expected" ]; then
    echo "error: checksum mismatch for AgoraAudio_iOS-$VERSION.zip" >&2
    echo "       expected $expected" >&2
    echo "       actual   $actual" >&2
    exit 1
fi

unzip -q "$WORK/sdk.zip" -d "$WORK/extracted"

rm -rf "$DEST"
mkdir -p "$DEST"

# Only the "RtcBasic" subspec: the API surface (AgoraRtcKit) plus every @rpath entry `otool -L`
# reports on AgoraRtcKit's own binary, confirmed against both the device and simulator slices of
# the voice build. Notably *no* video_dec — the voice engine is built without the video pipeline,
# which is what makes this pod worth binding separately at all. The other xcframeworks in the zip
# (AI echo cancellation, AI noise suppression, audio beauty, spatial audio) are optional feature
# plugins AgoraRtcKit does not itself depend on — same reasoning as voice-rtc-basic vs. voice-sdk
# on Android, see src/Net.Agora.Voice.Android in the sibling Net.Agora.Android repository.
#
# AgoraffmpegExtension is the exception: from 4.7.0 AgoraRtcKit no longer links an ffmpeg framework
# at all, so otool does not list it, but it replaced Agoraffmpeg in the pod's own RtcBasic subspec
# and the engine loads it itself. Leaving it out would be invisible until a feature needed it.
for framework in AgoraRtcKit aosl Agorafdkaac AgoraffmpegExtension AgoraSoundTouch; do
    if [ ! -d "$WORK/extracted/$framework.xcframework" ]; then
        echo "error: $framework.xcframework not found in the downloaded SDK" >&2
        exit 1
    fi
    cp -R "$WORK/extracted/$framework.xcframework" "$DEST/"
done

echo "Staged $(ls "$DEST" | wc -l | tr -d ' ') frameworks into $DEST"
echo "No maccatalyst slice is shipped for this SDK (ios-arm64 and simulator only), so there is no"
echo "Net.Agora.Voice.Mac project. See README.md."

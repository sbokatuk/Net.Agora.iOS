#!/usr/bin/env bash
set -euo pipefail

# Stages the optional RTC feature extensions' xcframeworks into
# src/Net.Agora.Extensions.<Name>.iOS/lib.
#
# The same archives as fetch-video.sh and fetch-voice.sh, verified against the SHA-256s already
# recorded in build/checksums.txt for those SDKs — nothing new to pin. Agora ships every extension
# inside AgoraRtcEngine_iOS, alongside AgoraRtcKit, at the same version, and the four audio ones
# inside AgoraAudio_iOS as well.
#
# Those four come from AgoraAudio_iOS, because they have to load in a Voice app as well as a Video
# one. From 4.7.0 every framework in AgoraRtcEngine_iOS links @rpath/Agoraffmpeg.framework, which
# the voice build no longer ships, so taking them from that archive kills a Voice app at launch
# with a dyld failure. The AgoraAudio_iOS builds link only what both RTC packages carry, and
# import the same three AgoraRtcKit symbols, which both builds of AgoraRtcKit export.
#
# Usage:
#   ./build/fetch-extensions.sh            # the version pinned in Directory.Build.props
#   ./build/fetch-extensions.sh 4.6.2      # an explicit version
#
# The package list comes from build/packages.tsv (its Extensions.* rows) so that adding a package
# is still "a row in the .tsv and a project under src/". What lives *here* is the mapping from a
# package's name to the framework(s) it carries, which is not derivable — Agora's marketing names
# and its framework names do not match (VirtualBackground ships AgoraVideoSegmentationExtension).
# A row with no mapping is a hard error rather than a silently empty package.

cd "$(dirname "$0")"

ROOT="$(cd .. && pwd)"
CHECKSUMS="$ROOT/build/checksums.txt"

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

# The framework(s) each package ships, echoed space separated. Keep in step with each project's
# NativeReference list — tests/Net.Agora.iOS.PackageTests holds the packed result to the same
# names, so a mismatch fails there rather than shipping an empty package.
frameworks_for() {
    case "$1" in
        Extensions.Ains)                 echo "AgoraAiNoiseSuppressionExtension" ;;
        Extensions.Aiaec)                echo "AgoraAiEchoCancellationExtension" ;;
        Extensions.AudioBeauty)          echo "AgoraAudioBeautyExtension" ;;
        Extensions.SpatialAudio)         echo "AgoraSpatialAudioExtension" ;;
        Extensions.VirtualBackground)    echo "AgoraVideoSegmentationExtension" ;;
        Extensions.ContentInspect)       echo "AgoraContentInspectExtension" ;;
        Extensions.ClearVision)          echo "AgoraClearVisionExtension" ;;
        Extensions.FaceCapture)          echo "AgoraFaceCaptureExtension" ;;
        Extensions.FaceDetection)        echo "AgoraFaceDetectionExtension" ;;
        Extensions.VideoQualityAnalyzer) echo "AgoraVideoQualityAnalyzerExtension" ;;
        Extensions.VideoEncoder)         echo "AgoraVideoEncoderExtension" ;;
        Extensions.Av1Encoder)           echo "AgoraVideoAv1EncoderExtension" ;;
        *)                               echo "" ;;
    esac
}

# The archive each package's frameworks come from. See the comment at the top.
archive_for() {
    case "$1" in
        Extensions.Ains|Extensions.Aiaec|Extensions.AudioBeauty|Extensions.SpatialAudio)
            echo "AgoraAudio_iOS" ;;
        *)  echo "AgoraRtcEngine_iOS" ;;
    esac
}

packages=$(grep -v '^#' "$ROOT/build/packages.tsv" | grep -v '^[[:space:]]*$' | cut -f1 | grep '^Extensions\.' || true)

if [ -z "${packages}" ]; then
    echo "error: build/packages.tsv lists no Extensions.* packages" >&2
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

for artifact in AgoraRtcEngine_iOS AgoraAudio_iOS; do
    expected=$(sed -n "s/^$artifact-$VERSION[[:space:]]\{1,\}\([0-9a-f]\{64\}\).*/\1/p" "$CHECKSUMS" | head -1)
    if [ -z "$expected" ]; then
        echo "error: no SHA-256 recorded for $artifact-$VERSION in $CHECKSUMS" >&2
        echo "       see the instructions at the top of that file for how to add one" >&2
        exit 1
    fi

    echo "Fetching $artifact $VERSION for its extensions..."
    curl -fL -o "$WORK/$artifact.zip" "https://download.agora.io/sdk/release/$artifact-$VERSION.zip"

    actual=$(sha256_of "$WORK/$artifact.zip")
    if [ "$actual" != "$expected" ]; then
        echo "error: checksum mismatch for $artifact-$VERSION.zip" >&2
        echo "       expected $expected" >&2
        echo "       actual   $actual" >&2
        exit 1
    fi

    unzip -q "$WORK/$artifact.zip" -d "$WORK/$artifact"
done

staged=0
for package in $packages; do
    frameworks=$(frameworks_for "$package")
    if [ -z "$frameworks" ]; then
        echo "error: build/packages.tsv lists $package, but frameworks_for() has no mapping for it" >&2
        echo "       add one to this script — see the comment at the top" >&2
        exit 1
    fi

    name="${package#Extensions.}"
    dest="$ROOT/src/Net.Agora.Extensions.$name.iOS/lib"
    extracted="$WORK/$(archive_for "$package")"

    if [ ! -d "$ROOT/src/Net.Agora.Extensions.$name.iOS" ]; then
        echo "error: build/packages.tsv lists $package, but src/Net.Agora.Extensions.$name.iOS does not exist" >&2
        exit 1
    fi

    rm -rf "$dest"
    mkdir -p "$dest"

    for framework in $frameworks; do
        if [ ! -d "$extracted/$framework.xcframework" ]; then
            echo "error: $framework.xcframework not found in the downloaded SDK (for $package)" >&2
            exit 1
        fi
        cp -R "$extracted/$framework.xcframework" "$dest/"
    done

    echo "  $package: $frameworks"
    staged=$((staged + 1))
done

echo "Staged $staged extension packages' frameworks."

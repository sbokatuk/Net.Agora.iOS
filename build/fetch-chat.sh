#!/usr/bin/env bash
set -euo pipefail

# Downloads the Agora Chat (IM) iOS SDK and stages the frameworks Net.Agora.Chat.iOS needs into
# src/Net.Agora.Chat.iOS/lib.
#
# Same mechanics as fetch-video.sh — the zip behind the CocoaPods source URL, downloaded directly
# and verified against build/checksums.txt, no CocoaPods or Ruby required — with one difference:
# this product takes two archives. Agora_Chat_iOS ships only AgoraChat.xcframework and declares
# AgoraInfra_iOS as a pod dependency for aosl, which `otool -L` confirms AgoraChat links at
# @rpath/aosl.framework/aosl. The RTC pods bundle their own copy of aosl; this one does not.
#
# The two archives are versioned independently, so both versions are pinned in
# Directory.Build.props and both are checksummed.
#
# Usage:
#   ./build/fetch-chat.sh                  # the versions pinned in Directory.Build.props
#   ./build/fetch-chat.sh 1.4.0            # an explicit Chat version, pinned Infra version
#   ./build/fetch-chat.sh 1.4.0 1.3.9      # both explicit
#
# The pod is Agora_Chat_iOS. Note the underscores: the AgoraChat pod on CocoaPods trunk is the
# abandoned 3.8.x Easemob line and is not this SDK. Check what is actually published before
# bumping either pin:
#   curl -s https://trunk.cocoapods.org/api/v1/pods/Agora_Chat_iOS | python3 -m json.tool
#   curl -s https://trunk.cocoapods.org/api/v1/pods/AgoraInfra_iOS | python3 -m json.tool

cd "$(dirname "$0")"

ROOT="$(cd .. && pwd)"
CHECKSUMS="$ROOT/build/checksums.txt"
DEST="$ROOT/src/Net.Agora.Chat.iOS/lib"

read_pin() {
    sed -n "s:.*<$1>\(.*\)</$1>.*:\1:p" "$ROOT/Directory.Build.props" | head -1
}

VERSION="${1:-}"
if [ -z "${VERSION}" ]; then
    VERSION=$(read_pin AgoraChatIosVersion)
fi

INFRA_VERSION="${2:-}"
if [ -z "${INFRA_VERSION}" ]; then
    INFRA_VERSION=$(read_pin AgoraChatInfraIosVersion)
fi

if [ -z "${VERSION}" ] || [ -z "${INFRA_VERSION}" ]; then
    echo "error: could not read AgoraChatIosVersion / AgoraChatInfraIosVersion from" >&2
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
# the shared staging directory. Both archives unpack xcframeworks at their top level, so they can
# share one directory without colliding.
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

    unzip -q "$WORK/$artifact.zip" -d "$WORK/extracted"
}

fetch_archive Agora_Chat_iOS "$VERSION"
fetch_archive AgoraInfra_iOS "$INFRA_VERSION"

rm -rf "$DEST"
mkdir -p "$DEST"

# Everything both archives contain: the API surface (AgoraChat) and aosl, its one @rpath
# dependency. Chat has no optional feature plugins to exclude, unlike the RTC products.
for framework in AgoraChat aosl; do
    if [ ! -d "$WORK/extracted/$framework.xcframework" ]; then
        echo "error: $framework.xcframework not found in the downloaded SDK" >&2
        exit 1
    fi
    cp -R "$WORK/extracted/$framework.xcframework" "$DEST/"
done

echo "Staged $(ls "$DEST" | wc -l | tr -d ' ') frameworks into $DEST"
echo "Both archives ship ios-arm64 and ios-arm64_x86_64-simulator only — no visionOS slices to"
echo "strip (unlike Signaling) and no maccatalyst slice, so there is no Net.Agora.Chat.Mac"
echo "project. See README.md."

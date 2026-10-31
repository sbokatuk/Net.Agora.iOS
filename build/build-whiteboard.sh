#!/usr/bin/env bash
set -euo pipefail

# Builds Whiteboard.xcframework from netless's source and stages it, with its JavaScript resource
# bundle, into src/Net.Agora.Whiteboard.iOS/lib.
#
# Every other product in this repository is fetched: Agora publishes a prebuilt .xcframework behind
# a download.agora.io URL and build/fetch-*.sh verifies its SHA-256. The Interactive Whiteboard has
# no such artifact. netless ships it as a CocoaPods *source* pod — the podspec points at a git tag,
# not a zip — so the only way to get a binary is to build one, which is what this does.
#
# Usage:
#   ./build/build-whiteboard.sh              # the version pinned in Directory.Build.props
#   ./build/build-whiteboard.sh 2.16.137     # an explicit version
#   ./build/build-whiteboard.sh --relock     # rewrite the lockfile for the pin, and build nothing
#
# Requires Xcode. No CocoaPods and no Ruby: the repository carries a Package.swift, so xcodebuild
# resolves NTLBridge and White_YYModel itself. It does so at the commits in
# native/Whiteboard/Package.resolved, because netless's manifest asks for them by version range and
# ships no lockfile: see build/build-fastboard.sh for what that costs when it floats.
#
# Check what is published before bumping the pin:
#   curl -s https://trunk.cocoapods.org/api/v1/pods/Whiteboard | python3 -m json.tool

cd "$(dirname "$0")"

ROOT="$(cd .. && pwd)"
CHECKSUMS="$ROOT/build/checksums.txt"
DEST="$ROOT/src/Net.Agora.Whiteboard.iOS/lib"
LOCKFILE="$ROOT/native/Whiteboard/Package.resolved"
REPO="netless-io/Whiteboard-iOS"

RELOCK=""
if [ "${1:-}" = "--relock" ]; then
    RELOCK=1
    shift
fi

VERSION="${1:-}"
if [ -z "${VERSION}" ]; then
    VERSION=$(sed -n 's:.*<AgoraWhiteboardIosVersion>\(.*\)</AgoraWhiteboardIosVersion>.*:\1:p' "$ROOT/Directory.Build.props" | head -1)
fi

if [ -z "${VERSION}" ]; then
    echo "error: could not read AgoraWhiteboardIosVersion from $ROOT/Directory.Build.props" >&2
    exit 1
fi

# The version becomes a git ref and a path, so reject anything exotic up front.
case "$VERSION" in
    *[!A-Za-z0-9._-]*)
        echo "error: invalid version '$VERSION'" >&2
        exit 1
        ;;
esac

# The trust anchor, and the reason build/checksums.txt has a second entry form. A tag is not a
# fingerprint — it is a mutable pointer, and netless has re-tagged before — so what is pinned is
# the commit the tag resolved to when the version was recorded. A moved tag fails here instead of
# silently building different source.
expected=$(sed -n "s|^git:$REPO@$VERSION[[:space:]]\{1,\}\([0-9a-f]\{40\}\).*|\1|p" "$CHECKSUMS" | head -1)
if [ -z "$expected" ]; then
    echo "error: no commit recorded for git:$REPO@$VERSION in $CHECKSUMS" >&2
    echo "       see the instructions at the top of that file for how to add one" >&2
    exit 1
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

echo "Cloning $REPO at $VERSION..."
git clone --quiet --depth 1 --branch "$VERSION" "https://github.com/$REPO.git" "$WORK/src"

actual=$(git -C "$WORK/src" rev-parse HEAD)
if [ "$actual" != "$expected" ]; then
    echo "error: $REPO tag $VERSION points at a different commit than recorded" >&2
    echo "       expected $expected" >&2
    echo "       actual   $actual" >&2
    exit 1
fi

# A dangling symlink to Example/Pods/Pods.xcodeproj, which the shallow clone does not contain.
# xcodebuild refuses to even list the schemes while it is there.
rm -f "$WORK/src/_Pods.xcodeproj"

if [ -n "$RELOCK" ]; then
    ( cd "$WORK/src" && swift package resolve )
    cp "$WORK/src/Package.resolved" "$LOCKFILE"
    echo "Re-locked $LOCKFILE"
    exit 0
fi

cp "$LOCKFILE" "$WORK/src/Package.resolved"

# Two archives, one per platform. SKIP_INSTALL=NO is what puts the built objects in the archive at
# all; BUILD_LIBRARY_FOR_DISTRIBUTION makes the Swift half of the target emit a stable module
# interface, without which a consumer built by a different Swift compiler cannot link it.
for platform in iOS "iOS Simulator"; do
    slug=$(printf '%s' "$platform" | tr 'A-Z ' 'a-z-')
    echo "Archiving for $platform..."
    # From the package directory: xcodebuild finds a Package.swift the way it finds an .xcodeproj,
    # by looking in the working directory, and this script's own is build/.
    ( cd "$WORK/src" && xcodebuild archive \
        -scheme Whiteboard \
        -destination "generic/platform=$platform" \
        -archivePath "$WORK/$slug" \
        -derivedDataPath "$WORK/dd" \
        -onlyUsePackageVersionsFromResolvedFile \
        SKIP_INSTALL=NO \
        BUILD_LIBRARY_FOR_DISTRIBUTION=YES ) \
        > "$WORK/$slug.log" 2>&1 || {
            echo "error: xcodebuild failed for $platform — see below" >&2
            tail -40 "$WORK/$slug.log" >&2
            exit 1
        }

    objects="$WORK/$slug.xcarchive/Products/$HOME/Objects"
    if [ ! -d "$objects" ]; then
        # The path embeds $HOME because a Swift package target has no install location of its own;
        # find it rather than guess if Xcode ever changes that.
        objects=$(find "$WORK/$slug.xcarchive/Products" -type d -name Objects -print -quit)
    fi

    if [ -z "$objects" ] || [ ! -d "$objects" ]; then
        echo "error: no Objects directory in the $platform archive" >&2
        exit 1
    fi

    # A Swift package's static target archives as one .o per target — Whiteboard's own, plus
    # NTLBridge and White_YYModel — and no linked library. libtool makes the single .a an
    # xcframework can carry; without the two dependencies in it, every consumer would have to
    # rediscover and add them.
    mkdir -p "$WORK/out/$slug"
    libtool -static -o "$WORK/out/$slug/libWhiteboard.a" "$objects"/*.o
done

# The headers are along for the ride: this binding is hand-written against them (see
# ApiDefinition.cs) and nothing compiles against the copy inside the xcframework. They are included
# because -create-xcframework requires a headers directory for a library, and because a reader
# unpacking the package should be able to see what the binary exposes.
mkdir -p "$WORK/headers"
cp -RL "$WORK/src/Whiteboard/Classes/include/." "$WORK/headers/"

rm -rf "$DEST"
mkdir -p "$DEST"

xcodebuild -create-xcframework \
    -library "$WORK/out/ios/libWhiteboard.a" -headers "$WORK/headers" \
    -library "$WORK/out/ios-simulator/libWhiteboard.a" -headers "$WORK/headers" \
    -output "$DEST/Whiteboard.xcframework" > /dev/null

# The board is a web view, and this bundle is the page it loads. Without it the view comes up blank
# with no error — the single most confusing way this package could fail, which is why it is staged
# here rather than left to the consumer.
bundle=$(find "$WORK/dd" -type d -name 'Whiteboard_Whiteboard.bundle' -print -quit)
if [ -z "$bundle" ]; then
    echo "error: Whiteboard_Whiteboard.bundle was not produced — the board would render blank" >&2
    exit 1
fi
# Keep SwiftPM's own name. The library locates it through SwiftPM's generated module-bundle
# finder, which looks for "Whiteboard_Whiteboard.bundle" by that exact string — visible in the
# built library with `strings`. Renaming it to something tidier makes the board render blank.
cp -R "$bundle" "$DEST/Whiteboard_Whiteboard.bundle"

echo "Staged $DEST:"
ls "$DEST"

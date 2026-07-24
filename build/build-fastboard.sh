#!/usr/bin/env bash
set -euo pipefail

# Builds AgoraFastboardBridge.xcframework — netless's Fastboard plus this repository's
# Objective-C-facing shim — and stages it, with Fastboard's asset bundles, into
# src/Net.Agora.Fastboard.iOS/lib.
#
# Two things make this the most involved build script here.
#
# Fastboard is Swift, and a .NET binding needs Objective-C. Its public API is @objc-annotated
# already, but a Swift @objc class registers under a mangled, module-qualified runtime name
# (_TtC9Fastboard8FastRoom) that a binding would have to name literally and that changes with the
# module name. So native/AgoraFastboardBridge wraps it in types with stable @objc(...) names, and
# that shim — not Fastboard — is what gets bound. It also flattens the one API that cannot cross
# into Objective-C at all: joinRoom's Result<WhiteRoom, FastRoomError>.
#
# And like the Interactive Whiteboard, there is no binary to fetch: netless publishes a CocoaPods
# source pod, so this builds one. The shim is an ordinary Swift package that depends on
# fastboard-iOS at a pinned revision; xcodebuild resolves the rest, including Whiteboard-iOS.
#
# Usage:
#   ./build/build-fastboard.sh            # the version pinned in Directory.Build.props
#   ./build/build-fastboard.sh 1.4.5      # an explicit version
#
# Requires Xcode. No CocoaPods and no Ruby.

cd "$(dirname "$0")"

ROOT="$(cd .. && pwd)"
CHECKSUMS="$ROOT/build/checksums.txt"
DEST="$ROOT/src/Net.Agora.Fastboard.iOS/lib"
BRIDGE="$ROOT/native/AgoraFastboardBridge"
REPO="netless-io/fastboard-iOS"

VERSION="${1:-}"
if [ -z "${VERSION}" ]; then
    VERSION=$(sed -n 's:.*<AgoraFastboardIosVersion>\(.*\)</AgoraFastboardIosVersion>.*:\1:p' "$ROOT/Directory.Build.props" | head -1)
fi

if [ -z "${VERSION}" ]; then
    echo "error: could not read AgoraFastboardIosVersion from $ROOT/Directory.Build.props" >&2
    exit 1
fi

case "$VERSION" in
    *[!A-Za-z0-9._-]*)
        echo "error: invalid version '$VERSION'" >&2
        exit 1
        ;;
esac

revision=$(sed -n "s|^git:$REPO@$VERSION[[:space:]]\{1,\}\([0-9a-f]\{40\}\).*|\1|p" "$CHECKSUMS" | head -1)
if [ -z "$revision" ]; then
    echo "error: no commit recorded for git:$REPO@$VERSION in $CHECKSUMS" >&2
    echo "       see the instructions at the top of that file for how to add one" >&2
    exit 1
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# The shim's sources are this repository's; only the manifest is generated, so that the pinned
# revision has exactly one home (build/checksums.txt).
mkdir -p "$WORK/bridge"
cp -R "$BRIDGE/Sources" "$WORK/bridge/Sources"
sed "s|@FASTBOARD_REVISION@|$revision|" "$BRIDGE/Package.swift.in" > "$WORK/bridge/Package.swift"

# No BUILD_LIBRARY_FOR_DISTRIBUTION here, unlike build-whiteboard.sh. It makes the compiler emit a
# textual .swiftinterface and then verify it, and Fastboard's own does not verify: the module named
# Fastboard also contains a class named Fastboard, so every qualified name in its interface
# ("Fastboard.FastRoom") resolves to the class and fails with "not a member type of class
# 'Fastboard.Fastboard'". That is upstream's defect, and it is also not a problem worth working
# around — the flag exists so a *Swift* consumer built by a different compiler can import the
# module, and nothing here does. Fastboard, Whiteboard and the shim are compiled together by one
# compiler into one static library, and what a .NET binding consumes is the Objective-C surface,
# which Swift's ABI stability already guarantees at runtime.
for platform in iOS "iOS Simulator"; do
    slug=$(printf '%s' "$platform" | tr 'A-Z ' 'a-z-')
    echo "Archiving AgoraFastboardBridge for $platform..."
    ( cd "$WORK/bridge" && xcodebuild archive \
        -scheme AgoraFastboardBridge \
        -destination "generic/platform=$platform" \
        -archivePath "$WORK/$slug" \
        -derivedDataPath "$WORK/dd" \
        SKIP_INSTALL=NO ) \
        > "$WORK/$slug.log" 2>&1 || {
            echo "error: xcodebuild failed for $platform — compiler diagnostics below" >&2
            # The compiler's own lines, not xcodebuild's closing summary: a failed Swift build ends
            # with a page-long re-listing of every module on the command line, which buries them.
            grep -E "error:|warning: .*deprecated" "$WORK/$slug.log" | sort -u | head -30 >&2 || tail -20 "$WORK/$slug.log" >&2
            exit 1
        }

    objects=$(find "$WORK/$slug.xcarchive/Products" -type d -name Objects -print -quit)
    if [ -z "$objects" ]; then
        echo "error: no Objects directory in the $platform archive" >&2
        exit 1
    fi

    # One .o per target — the shim, Fastboard, Whiteboard, NTLBridge, White_YYModel — linked into
    # the single .a the xcframework carries.
    mkdir -p "$WORK/out/$slug"
    libtool -static -o "$WORK/out/$slug/libAgoraFastboardBridge.a" "$objects"/*.o
done

# The generated header is the whole point of the shim: it is the Objective-C view of the @objc
# types, and what src/Net.Agora.Fastboard.iOS/ApiDefinition.cs is written against.
header=$(find "$WORK/dd" -name 'AgoraFastboardBridge-Swift.h' -print -quit)
if [ -z "$header" ]; then
    echo "error: AgoraFastboardBridge-Swift.h was not generated — nothing would be bindable" >&2
    exit 1
fi

mkdir -p "$WORK/headers"
cp "$header" "$WORK/headers/"

rm -rf "$DEST"
mkdir -p "$DEST"

xcodebuild -create-xcframework \
    -library "$WORK/out/ios/libAgoraFastboardBridge.a" -headers "$WORK/headers" \
    -library "$WORK/out/ios-simulator/libAgoraFastboardBridge.a" -headers "$WORK/headers" \
    -output "$DEST/AgoraFastboardBridge.xcframework" > /dev/null

# The resource bundles, under the names SwiftPM's generated finders look them up by — the board's
# JavaScript and Fastboard's own toolbar icons and localised strings. A tidier name makes the
# toolbar render as empty buttons, or the board blank.
for bundle in Whiteboard_Whiteboard Fastboard_Fastboard; do
    found=$(find "$WORK/dd" -type d -name "$bundle.bundle" -print -quit)
    if [ -z "$found" ]; then
        echo "error: $bundle.bundle was not produced" >&2
        exit 1
    fi
    cp -R "$found" "$DEST/$bundle.bundle"
done

# The header is kept beside the xcframework too, so a reader can diff ApiDefinition.cs against what
# the shim actually exposes without unpacking the package.
cp "$header" "$DEST/"

echo "Staged $DEST:"
ls "$DEST"

#!/bin/sh

set -e

# Builds and packs the Agora iOS binding packages listed in build/packages.tsv.
#
# Usage:
#   ./build/BuildNugets.sh                       # every package, each at its own version (below)
#   ./build/BuildNugets.sh --track chat          # only the packages on one release track
#   ./build/BuildNugets.sh --suffix beta.12.34   # every package, with a prerelease suffix appended
#   ./build/BuildNugets.sh --track rtc --suffix beta.12.34   # both, in either order
#
# --track scopes the pack to one release track (see build/tracks.tsv): a chat release builds only
# the Chat package, not the whole repository. Releases pass it so a tag publishes only its own
# track; omit it and every package is packed, which is what CI and the pull-request beta want.
#
# Each package packs at its own <VersionPrefix> from Directory.Build.props: the packages sit on
# independent native version lines (RTC 4.6.x, RTM 2.2.x), so no single version can be stamped
# across the set — which is why there is no way to pass one. Releases publish whatever versions
# the pins say, and nuget.org's --skip-duplicate makes republishing an unchanged version a no-op.
#
# Run build/fetch-video.sh (or the equivalent for whichever package you are packing) first — the
# xcframeworks are not fetched by this script.
#
# Packages are written to ../artifacts.
#
# Each .NET SDK's iOS workload ships reference packs for only two target frameworks - the .NET 9
# band covers net8/net9, the .NET 10 band covers net9/net10 - so this runs two passes and merges
# them. The repository's global.json pins the .NET 9 SDK, so the second pass is invoked from a
# scratch directory carrying its own global.json, since the SDK is resolved from the working
# directory.

cd "$(dirname "$0")"

SUFFIX=""
TRACK=""
while [ $# -gt 0 ]; do
    case "$1" in
        --suffix)
            SUFFIX="${2:?--suffix needs a value}"
            shift 2
            ;;
        --track)
            TRACK="${2:?--track needs a value}"
            shift 2
            ;;
        *)
            echo "error: unknown argument '$1' (a single version cannot be stamped across" >&2
            echo "       independent version lines — use --suffix for prereleases, --track to" >&2
            echo "       scope to one release track)" >&2
            exit 2
            ;;
    esac
done

ROOT="$(cd .. && pwd)"
OUTPUT="$ROOT/artifacts"

PASS1_BAND="net9"
PASS2_BAND="net10"
PASS2_SDK="10.0.100"

PACKAGES=$(grep -v '^#' packages.tsv | grep -v '^[[:space:]]*$' | cut -f1)

if [ -z "$PACKAGES" ]; then
    echo "error: no packages found in build/packages.tsv" >&2
    exit 1
fi

# Scope to one track's packages when asked. The track's ids come from build/tracks.tsv, where a
# trailing "*" is a prefix match (Extensions.* is the twelve extension packages).
if [ -n "$TRACK" ]; then
    PATTERNS=$(awk -F'	' -v t="$TRACK" '$1 == t { print $3 }' tracks.tsv)
    if [ -z "$PATTERNS" ]; then
        echo "error: unknown track '$TRACK' (not in build/tracks.tsv)" >&2
        exit 2
    fi

    SELECTED=""
    for package in $PACKAGES; do
        for pattern in $PATTERNS; do
            case "$pattern" in
                *'*')
                    prefix=${pattern%'*'}
                    case "$package" in "$prefix"*) SELECTED="$SELECTED $package" ;; esac
                    ;;
                "$package")
                    SELECTED="$SELECTED $package"
                    ;;
            esac
        done
    done

    if [ -z "$SELECTED" ]; then
        echo "error: track '$TRACK' matches no package in this repository" >&2
        exit 1
    fi

    PACKAGES="$SELECTED"
    echo "==> track '$TRACK':$PACKAGES"
fi

VERSION_ARG=""
if [ -n "$SUFFIX" ]; then
    case "$SUFFIX" in
        *[!A-Za-z0-9.-]*)
            echo "error: invalid suffix '$SUFFIX'" >&2
            exit 1
            ;;
    esac
    VERSION_ARG="-p:VersionSuffix=$SUFFIX"
fi

# NuGet.config declares ./artifacts as a package source, and restore fails outright with NU1301 if
# a local source directory is missing - before anything here has had a chance to create it as an
# output directory. A fresh clone only has it because an empty .gitkeep is committed, so make the
# build independent of that surviving.
mkdir -p "$OUTPUT"

PASS1_DIR="$OUTPUT/.net9-pass"
PASS2_DIR="$OUTPUT/.net10-pass"
rm -rf "$PASS1_DIR" "$PASS2_DIR"

SDK10_DIR="$(mktemp -d)"
trap 'rm -rf "$SDK10_DIR"' EXIT
cat > "$SDK10_DIR/global.json" <<EOF
{ "sdk": { "version": "$PASS2_SDK", "rollForward": "latestFeature" } }
EOF

for package in $PACKAGES; do
    project="$ROOT/src/Net.Agora.$package.iOS/Net.Agora.$package.iOS.csproj"

    if [ ! -f "$project" ]; then
        echo "error: $project does not exist, but build/packages.tsv lists $package" >&2
        exit 1
    fi

    echo "==> packing Net.Agora.$package.iOS ($PASS1_BAND band)"
    dotnet pack "$project" \
        -c Release \
        -p:AgoraSdkBand="$PASS1_BAND" \
        $VERSION_ARG \
        -o "$PASS1_DIR"

    echo "==> packing Net.Agora.$package.iOS ($PASS2_BAND band)"
    (cd "$SDK10_DIR" && dotnet pack "$project" \
        -c Release \
        -p:AgoraSdkBand="$PASS2_BAND" \
        $VERSION_ARG \
        -o "$PASS2_DIR")
done

echo "==> merging target frameworks"
python3 "$ROOT/build/merge-packages.py" "$PASS1_DIR" "$PASS2_DIR" "$OUTPUT"

rm -rf "$PASS1_DIR" "$PASS2_DIR"

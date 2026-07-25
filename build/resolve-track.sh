#!/usr/bin/env bash
set -euo pipefail

# Maps a release tag to its track, by longest-prefix match against build/tracks.tsv.
#
#   ./build/resolve-track.sh v4.6.3.4        -> rtc
#   ./build/resolve-track.sh chat-v1.4.0.1   -> chat
#
# Longest-prefix, so the main track's bare "v" does not capture "chat-v...". Exits non-zero with a
# message if the tag matches no track — which is what stops release.yml from publishing a tag whose
# scope it cannot determine.

tag="${1:?usage: resolve-track.sh <tag>}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TRACKS="$ROOT/build/tracks.tsv"

best_prefix=""
best_track=""
while IFS="$(printf '\t')" read -r track prefix _rest; do
    case "$track" in ''|'#'*) continue ;; esac
    case "$tag" in
        "$prefix"*)
            if [ "${#prefix}" -gt "${#best_prefix}" ]; then
                best_prefix="$prefix"
                best_track="$track"
            fi
            ;;
    esac
done < "$TRACKS"

if [ -z "$best_track" ]; then
    echo "error: tag '$tag' matches no track prefix in build/tracks.tsv" >&2
    exit 1
fi

printf '%s\n' "$best_track"

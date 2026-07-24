#!/usr/bin/env bash
# The only parser of Directory.Build.props for shell callers. Source this, don't execute it.
#
#   . build/pins.sh
#   echo "$AGORA_VIDEO_IOS_VERSION"

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export AGORA_REPO_ROOT
AGORA_REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

export AGORA_VIDEO_IOS_VERSION
AGORA_VIDEO_IOS_VERSION="$(grep -oE '<AgoraVideoIosVersion>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraVideoIosVersion>//')"
export AGORA_VIDEO_BINDING_REVISION
AGORA_VIDEO_BINDING_REVISION="$(grep -oE '<AgoraVideoBindingRevision>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraVideoBindingRevision>//')"
export AGORA_VIDEO_PACKAGE_VERSION="${AGORA_VIDEO_IOS_VERSION}.${AGORA_VIDEO_BINDING_REVISION}"

export AGORA_VOICE_IOS_VERSION
AGORA_VOICE_IOS_VERSION="$(grep -oE '<AgoraVoiceIosVersion>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraVoiceIosVersion>//')"
export AGORA_VOICE_BINDING_REVISION
AGORA_VOICE_BINDING_REVISION="$(grep -oE '<AgoraVoiceBindingRevision>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraVoiceBindingRevision>//')"
export AGORA_VOICE_PACKAGE_VERSION="${AGORA_VOICE_IOS_VERSION}.${AGORA_VOICE_BINDING_REVISION}"

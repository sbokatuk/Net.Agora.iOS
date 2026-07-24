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

export AGORA_SIGNALING_IOS_VERSION
AGORA_SIGNALING_IOS_VERSION="$(grep -oE '<AgoraSignalingIosVersion>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraSignalingIosVersion>//')"
export AGORA_SIGNALING_BINDING_REVISION
AGORA_SIGNALING_BINDING_REVISION="$(grep -oE '<AgoraSignalingBindingRevision>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraSignalingBindingRevision>//')"
export AGORA_SIGNALING_PACKAGE_VERSION="${AGORA_SIGNALING_IOS_VERSION}.${AGORA_SIGNALING_BINDING_REVISION}"

export AGORA_CHAT_IOS_VERSION
AGORA_CHAT_IOS_VERSION="$(grep -oE '<AgoraChatIosVersion>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraChatIosVersion>//')"
export AGORA_CHAT_INFRA_IOS_VERSION
AGORA_CHAT_INFRA_IOS_VERSION="$(grep -oE '<AgoraChatInfraIosVersion>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraChatInfraIosVersion>//')"
export AGORA_CHAT_BINDING_REVISION
AGORA_CHAT_BINDING_REVISION="$(grep -oE '<AgoraChatBindingRevision>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraChatBindingRevision>//')"
export AGORA_CHAT_PACKAGE_VERSION="${AGORA_CHAT_IOS_VERSION}.${AGORA_CHAT_BINDING_REVISION}"

export AGORA_WHITEBOARD_IOS_VERSION
AGORA_WHITEBOARD_IOS_VERSION="$(grep -oE '<AgoraWhiteboardIosVersion>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraWhiteboardIosVersion>//')"
export AGORA_WHITEBOARD_BINDING_REVISION
AGORA_WHITEBOARD_BINDING_REVISION="$(grep -oE '<AgoraWhiteboardBindingRevision>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraWhiteboardBindingRevision>//')"
export AGORA_WHITEBOARD_PACKAGE_VERSION="${AGORA_WHITEBOARD_IOS_VERSION}.${AGORA_WHITEBOARD_BINDING_REVISION}"

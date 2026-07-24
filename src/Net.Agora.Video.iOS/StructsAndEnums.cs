namespace Net.Agora.Video.iOS {

	// AgoraChannelProfile — AgoraEnumerates.h.
	public enum AgoraChannelProfile : long {
		Communication = 0,
		LiveBroadcasting = 1,
	}

	// AgoraClientRole — AgoraEnumerates.h.
	public enum AgoraClientRole : long {
		Broadcaster = 1,
		Audience = 2,
	}

	// AgoraErrorCode — AgoraEnumerates.h. Not exhaustive: only the values a consumer of this thin
	// wrapper needs to branch on. The SDK can report codes beyond these; cast to int for the rest.
	public enum AgoraErrorCode : long {
		NoError = 0,
		Failed = 1,
		InvalidArgument = 2,
		NotReady = 3,
		NotSupported = 4,
		TokenExpired = 109,
		InvalidToken = 110,
	}

	// AgoraUserOfflineReason — AgoraEnumerates.h.
	public enum AgoraUserOfflineReason : ulong {
		Quit = 0,
		Dropped = 1,
		BecomeAudience = 2,
	}

	// AgoraConnectionState — AgoraEnumerates.h.
	public enum AgoraConnectionState : long {
		Disconnected = 1,
		Connecting = 2,
		Connected = 3,
		Reconnecting = 4,
		Failed = 5,
	}

	// AUDIO_AINS_MODE — AgoraEnumerates.h. Renamed from the SDK's SHOUTING_CASE, which is a C
	// spelling with no place in a .NET surface; the values are unchanged.
	public enum AgoraAinsMode : long {
		Balanced = 0,
		Aggressive = 1,
		UltraLowLatency = 2,
	}

	// AgoraVoiceBeautifierPreset — AgoraEnumerates.h. Not exhaustive: the presets bound here are
	// the ones AgoraRtcEngineKit.h documents as generally applicable. The SDK defines ~20 more,
	// several of them Mandarin-specific; cast to the enum's underlying type for those.
	public enum AgoraVoiceBeautifierPreset : long {
		Off = 0x00000000,
		ChatBeautifierMagnetic = 0x01010100,
		ChatBeautifierFresh = 0x01010200,
		ChatBeautifierVitality = 0x01010300,
		TimbreTransformationVigorous = 0x01030100,
		TimbreTransformationDeep = 0x01030200,
		TimbreTransformationMellow = 0x01030300,
		TimbreTransformationFalsetto = 0x01030400,
		TimbreTransformationFull = 0x01030500,
		TimbreTransformationClear = 0x01030600,
		TimbreTransformationResounding = 0x01030700,
		TimbreTransformationRinging = 0x01030800,
	}

	// AgoraAudioEffectPreset — AgoraEnumerates.h. Not exhaustive, for the same reason as
	// AgoraVoiceBeautifierPreset.
	public enum AgoraAudioEffectPreset : long {
		Off = 0x00000000,
		RoomAcousticsKTV = 0x02010100,
		RoomAcousticsVocalConcert = 0x02010200,
		RoomAcousticsStudio = 0x02010300,
		RoomAcousticsPhonograph = 0x02010400,
		RoomAcousticsVirtualStereo = 0x02010500,
		RoomAcousticsSpacial = 0x02010600,
		RoomAcousticsEthereal = 0x02010700,
		RoomAcoustics3DVoice = 0x02010800,
		VoiceChangerEffectUncle = 0x02020100,
		VoiceChangerEffectOldMan = 0x02020200,
		VoiceChangerEffectBoy = 0x02020300,
		VoiceChangerEffectSister = 0x02020400,
		VoiceChangerEffectGirl = 0x02020500,
		VoiceChangerEffectPigKing = 0x02020600,
		VoiceChangerEffectHulk = 0x02020700,
		StyleTransformationRnB = 0x02030100,
		StyleTransformationPopular = 0x02030200,
		PitchCorrection = 0x02040100,
	}

	// AgoraVirtualBackgroundSourceType — AgoraEnumerates.h.
	public enum AgoraVirtualBackgroundSourceType : ulong {
		None = 0,
		Color = 1,
		Img = 2,
		Blur = 3,
		Video = 4,
	}

	// AgoraBlurDegree — AgoraEnumerates.h. Note the values start at 1.
	public enum AgoraBlurDegree : ulong {
		Low = 1,
		Medium = 2,
		High = 3,
	}

	// AgoraVideoDenoiserMode / AgoraVideoDenoiserLevel — AgoraEnumerates.h.
	public enum AgoraVideoDenoiserMode : ulong {
		Auto = 0,
		Manual = 1,
	}

	public enum AgoraVideoDenoiserLevel : ulong {
		HighQuality = 0,
		Fast = 1,
		Strong = 2,
	}

	// AgoraLowlightEnhanceMode / AgoraLowlightEnhanceLevel — AgoraEnumerates.h.
	public enum AgoraLowlightEnhanceMode : ulong {
		Auto = 0,
		Manual = 1,
	}

	public enum AgoraLowlightEnhanceLevel : ulong {
		HighQuality = 0,
		Fast = 1,
	}
}

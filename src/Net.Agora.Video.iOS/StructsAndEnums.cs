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
}

namespace Net.Agora.Signaling.iOS {

	// AgoraRtmClientConnectionState — AgoraRtmEnumerates.h. The same five states, with the same
	// values, as the RTC engine's AgoraConnectionState.
	public enum AgoraRtmClientConnectionState : long {
		Disconnected = 1,
		Connecting = 2,
		Connected = 3,
		Reconnecting = 4,
		Failed = 5,
	}

	// AgoraRtmChannelType — AgoraRtmEnumerates.h.
	public enum AgoraRtmChannelType : long {
		None = 0,
		Message = 1,
		Stream = 2,
		User = 3,
	}
}

namespace Net.Agora.Whiteboard.iOS {

	// WhiteRoomPhase — WhiteRoomCallbacks.h. Note the values: Connecting is 0, so there is no
	// "disconnected" zero to default to — a freshly constructed room reports Connecting.
	public enum WhiteRoomPhase : long {
		Connecting = 0,
		Connected = 1,
		Reconnecting = 2,
		Disconnecting = 3,
		Disconnected = 4,
	}

	// WhiteViewMode — WhiteBroadcastState.h. Whether this client's camera moves on its own, follows
	// the broadcaster's, or is the one others follow.
	public enum WhiteViewMode : long {
		Freedom = 0,
		Follower = 1,
		Broadcaster = 2,
	}
}

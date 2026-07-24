namespace Net.Agora.Fastboard.iOS {

	// AgoraFastboardPhase — AgoraFastboardBridge-Swift.h, mirroring Fastboard's own FastRoomPhase
	// and sharing its raw values.
	public enum AgoraFastboardPhase : long {
		Connecting = 0,
		Connected = 1,
		Reconnecting = 2,
		Disconnecting = 3,
		Disconnected = 4,

		// Fastboard's own sixth case, reported when the board answers with a phase it does not
		// recognise. The whiteboard SDK has no equivalent.
		Unknown = 5,
	}
}

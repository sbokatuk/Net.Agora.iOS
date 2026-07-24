namespace Net.Agora.Chat.iOS {

	// AgoraChatConnectionState — AgoraChatClientDelegate.h. Two states, not the RTC/RTM engines'
	// five: Chat reports only whether the socket is up, and reports login failures through the
	// login completion block instead.
	public enum AgoraChatConnectionState : long {
		Connected = 0,
		Disconnected = 1,
	}

	// AgoraChatLogLevel — AgoraChatOptions.h.
	public enum AgoraChatLogLevel : long {
		Debug = 0,
		Warning = 1,
		Error = 2,
	}

	// AgoraChatMessageBodyType — AgoraChatMessageBody.h. Note the values start at 1, not 0.
	public enum AgoraChatMessageBodyType : long {
		Text = 1,
		Image = 2,
		Video = 3,
		Location = 4,
		Voice = 5,
		File = 6,
		Cmd = 7,
		Custom = 8,
		Combine = 9,
	}

	// AgoraChatType — AgoraChatMessage.h. The kind of conversation a message belongs to.
	public enum AgoraChatType : long {
		Chat = 0,
		GroupChat = 1,
		ChatRoom = 2,
	}

	// AgoraChatMessageStatus — AgoraChatMessage.h.
	public enum AgoraChatMessageStatus : long {
		Pending = 0,
		Delivering = 1,
		Succeed = 2,
		Failed = 3,
	}

	// AgoraChatMessageDirection — AgoraChatMessage.h.
	public enum AgoraChatMessageDirection : long {
		Send = 0,
		Receive = 1,
	}

	// AgoraChatConversationType — AgoraChatConversation.h. Deliberately a separate enum from
	// AgoraChatType even though the three cases line up: that is how the SDK declares them, and
	// the two are not interchangeable in its own signatures.
	public enum AgoraChatConversationType : long {
		Chat = 0,
		GroupChat = 1,
		ChatRoom = 2,
	}
}

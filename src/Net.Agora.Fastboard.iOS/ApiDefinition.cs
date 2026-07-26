using System;
using ObjCRuntime;
using Foundation;
using UIKit;

namespace Net.Agora.Fastboard.iOS {

	// Hand-written against AgoraFastboardBridge-Swift.h — the header the Swift compiler generates
	// for native/AgoraFastboardBridge, staged beside the xcframework by
	// build/build-fastboard.sh so it can be diffed against this file.
	//
	// Unique among the bindings here in what it binds: not the SDK, but this repository's own
	// Objective-C shim over it. Fastboard is Swift, and although its public API is @objc-annotated,
	// a Swift @objc class registers under a mangled, module-qualified runtime name
	// (_TtC9Fastboard8FastRoom) that a binding would have to name literally and that changes with
	// the module name. The shim gives every type a stable @objc(...) name, and flattens the one
	// call that cannot cross into Objective-C at all — joinRoom's Result<WhiteRoom, FastRoomError>,
	// which becomes the delegate callbacks below.
	//
	// So the surface here is deliberately the shim's, not Fastboard's: what is missing is missing
	// from native/AgoraFastboardBridge first. Widen that, rebuild, and extend this to match.

	// The completion block on -setWritable:completion: — nil message means success. A message
	// rather than an NSError because that is what the shim hands back.
	public delegate void AgoraFastboardCompletionHandler ([NullAllowed] string errorMessage);

	// AgoraFastboardConfiguration. A flat property bag rather than Fastboard's nested
	// WhiteSdkConfiguration/WhiteRoomConfig pair — see the shim for why.
	[BaseType (typeof (NSObject))]
	[DisableDefaultCtor]
	public interface AgoraFastboardConfiguration {
		[Export ("initWithAppIdentifier:roomUUID:roomToken:userUID:")]
		NativeHandle Constructor (string appIdentifier, string roomUuid, string roomToken, string userUid);

		[Export ("appIdentifier")]
		string AppIdentifier { get; }

		[Export ("roomUUID")]
		string RoomUuid { get; }

		[Export ("roomToken")]
		string RoomToken { get; }

		[Export ("userUID")]
		string UserUid { get; }

		// One of netless's region keys, or empty for China — Fastboard's own default. A string
		// because the whiteboard SDKs take one on both platforms; Fastboard's Region enum is
		// Swift-only, and the shim maps between them.
		[Export ("region")]
		string Region { get; set; }
	}

	// AgoraFastboardDelegate. Every member is required: an @objc optional protocol member binds as
	// optional here too, and there is no callback a client can sensibly ignore.
	[Protocol]
	[Model]
	[BaseType (typeof (NSObject))]
	public interface AgoraFastboardDelegate {
		[Abstract]
		[Export ("fastboardDidJoin")]
		void FastboardDidJoin ();

		[Abstract]
		[Export ("fastboardDidFail:")]
		void FastboardDidFail (string message);

		[Abstract]
		[Export ("fastboardWasKicked:")]
		void FastboardWasKicked (string reason);

		[Abstract]
		[Export ("fastboardPhaseDidChange:")]
		void FastboardPhaseDidChange (AgoraFastboardPhase phase);
	}

	// AgoraFastboard. A whiteboard with Fastboard's built-in toolbar: add View to a view hierarchy,
	// then Join.
	[BaseType (typeof (NSObject))]
	[DisableDefaultCtor]
	public interface AgoraFastboard {
		[Export ("initWithConfiguration:")]
		NativeHandle Constructor (AgoraFastboardConfiguration configuration);

		// The board and its toolbar. Fastboard lays this out itself; give it a frame.
		[Export ("view", ArgumentSemantic.Strong)]
		UIView View { get; }

		[NullAllowed]
		[Export ("delegate", ArgumentSemantic.Weak)]
		AgoraFastboardDelegate Delegate { get; set; }

		// Asynchronous underneath: the outcome arrives on the delegate, not here. Fastboard's own
		// completion-handler overload takes a Swift Result, which is why the shim exposes this one.
		[Export ("join")]
		void Join ();

		[Export ("disconnect")]
		void Disconnect ();

		// SetWritableAsync — Task<string?>: the block's one parameter becomes the task's result
		// directly, so awaiting yields the error message, null meaning success — same null
		// convention as the callback. No ResultTypeName: on a single-parameter block the
		// generator ignores it and would emit the named class as an orphan (verified on Chat's
		// single-value handlers). Join is not async-able at all — its outcome arrives on the
		// delegate, per the comment on Join above.
		[Export ("setWritable:completion:")]
		[Async]
		void SetWritable (bool writable, [NullAllowed] AgoraFastboardCompletionHandler completion);

		// The tool, as one of the whiteboard SDK's appliance-name strings. Colour is a
		// three-element [r, g, b] array of 0..255 — netless's own shape, on both platforms.
		[Export ("setAppliance:color:strokeWidth:")]
		void SetAppliance (string applianceName, [NullAllowed] NSNumber [] color, [NullAllowed] NSNumber strokeWidth);

		[Export ("undo")]
		void Undo ();

		[Export ("redo")]
		void Redo ();

		[Export ("clear:")]
		void Clear (bool retainDocument);

		[Export ("refreshViewSize")]
		void RefreshViewSize ();
	}
}

using System;
using ObjCRuntime;
using Foundation;
using WebKit;

namespace Net.Agora.Whiteboard.iOS {

	// Hand-written against the real headers of the Whiteboard.xcframework build/build-whiteboard.sh
	// produces from netless's source — same reasoning as every other binding here: Objective
	// Sharpie's bundled clang cannot parse SDKs from current Xcode.
	//
	// Scoped to what the thin Net.Agora.Whiteboard client needs — create the SDK against a board
	// view, join a room, choose a tool and colour, navigate scenes, undo/redo, and the room's
	// lifecycle callbacks — not netless's full surface. The SDK ships 72 public headers: replay,
	// multi-window apps, PPT conversion, the synced key-value store, camera bounds, iframe bridges
	// and the RTC audio-mixer hooks are all deliberately unbound.
	//
	// This one is a *static* library rather than a dynamic framework, which is why the .csproj
	// leaves NoBindingEmbedding at its default: there is nothing to copy beside the assembly, it
	// links into the app.

	// WhiteObject — WhiteObject.h. The SDK's own model base class: everything that crosses the
	// JavaScript bridge derives from it, and it exists here only so the derived types have a base
	// to name.
	[BaseType (typeof (NSObject))]
	public interface WhiteObject {
	}

	// WhiteBoardView — WhiteBoardView.h. The board itself, and an ordinary UIView a caller adds to
	// a view hierarchy.
	//
	// Declared over WKWebView rather than its real base, NTLDWKWebView: that is netless's JS-bridge
	// subclass, whose surface nothing here calls, and skipping it costs nothing for a type this
	// binding only ever consumes. It does say what the board is, though — the whole SDK is a web
	// app in a web view, which is why the JavaScript bundle staged next to the xcframework is not
	// optional.
	[BaseType (typeof (WKWebView))]
	public interface WhiteBoardView {
		// No -init export: the generator already emits a parameterless constructor for a
		// [BaseType] that is not [DisableDefaultCtor], and declaring it again is a duplicate.

		// Set by joinRoomWithConfig: on success — the same instance the completion handler is
		// given, kept here because the view outlives any one reference to it.
		[NullAllowed]
		[Export ("room", ArgumentSemantic.Strong)]
		WhiteRoom Room { get; set; }

		// The SDK nudges the board up when the keyboard appears. Turn it off when the hosting
		// layout already does that.
		[Export ("disableKeyboardHandler")]
		bool DisableKeyboardHandler { get; set; }
	}

	// WhiteSdkConfiguration — WhiteSdkConfiguration.h. The ~20 remaining knobs (render engine,
	// PPT parameters, fonts, logger options, presentation viewport) stay unbound.
	[BaseType (typeof (WhiteObject))]
	[DisableDefaultCtor]
	public interface WhiteSdkConfiguration {
		[Export ("initWithApp:")]
		NativeHandle Constructor (string appIdentifier);

		[Export ("appIdentifier")]
		string AppIdentifier { get; set; }

		// The data-centre region — "cn-hz", "us-sv", "sg", "in-mum", "gb-lon". A room can only be
		// joined from the region it was created in.
		[NullAllowed]
		[Export ("region", ArgumentSemantic.Strong)]
		string Region { get; set; }

		[Export ("log")]
		bool Log { get; set; }

		// Multi-window mode, where the board hosts documents in movable windows rather than
		// drawing straight onto one canvas.
		[Export ("useMultiViews")]
		bool UseMultiViews { get; set; }
	}

	// WhiteRoomConfig — WhiteRoomConfig.h. Everything a join needs plus the two switches worth
	// having; the disable* family, camera bounds and window parameters stay unbound.
	[BaseType (typeof (WhiteObject))]
	[DisableDefaultCtor]
	public interface WhiteRoomConfig {
		[Export ("initWithUUID:roomToken:uid:")]
		NativeHandle Constructor (string uuid, string roomToken, string uid);

		[Export ("uuid")]
		string Uuid { get; }

		[Export ("roomToken")]
		string RoomToken { get; }

		[Export ("uid")]
		string Uid { get; }

		[NullAllowed]
		[Export ("region", ArgumentSemantic.Strong)]
		string Region { get; set; }

		// Join read-only — an observer who can see the board but not draw on it.
		[Export ("isWritable")]
		bool IsWritable { get; set; }
	}

	// WhiteReadonlyMemberState — WhiteMemberState.h. What the room reports the local tool to be.
	// Colours are three-element [r, g, b] arrays of 0..255 numbers, which is netless's own shape.
	[BaseType (typeof (WhiteObject))]
	public interface WhiteReadonlyMemberState {
		[Export ("currentApplianceName")]
		string CurrentApplianceName { get; }

		[Export ("strokeColor", ArgumentSemantic.Copy)]
		NSNumber [] StrokeColor { get; }

		[NullAllowed]
		[Export ("strokeWidth", ArgumentSemantic.Strong)]
		NSNumber StrokeWidth { get; }

		[NullAllowed]
		[Export ("textSize", ArgumentSemantic.Strong)]
		NSNumber TextSize { get; }
	}

	// WhiteMemberState — WhiteMemberState.h. The writable half: construct one, set what should
	// change and hand it to WhiteRoom.SetMemberState. Unset properties are left alone.
	//
	// Each property below redeclares a read-only one from the base as read-write, which is what
	// the header does. The generator emits them without `new` and the compiler notes the hiding
	// (CS0114, suppressed in the .csproj) — writing `new` here does not reach the generated code
	// and only earns a CS0109 for being unnecessary in this file.
	[BaseType (typeof (WhiteReadonlyMemberState))]
	public interface WhiteMemberState {
		[NullAllowed]
		[Export ("currentApplianceName", ArgumentSemantic.Copy)]
		string CurrentApplianceName { get; set; }

		[NullAllowed]
		[Export ("strokeColor", ArgumentSemantic.Copy)]
		NSNumber [] StrokeColor { get; set; }

		[NullAllowed]
		[Export ("strokeWidth", ArgumentSemantic.Strong)]
		NSNumber StrokeWidth { get; set; }

		[NullAllowed]
		[Export ("textSize", ArgumentSemantic.Strong)]
		NSNumber TextSize { get; set; }
	}

	// WhiteDisplayer — WhiteDisplayer.h. The base a room and a replayer share. Bound as an opaque
	// type: everything this binding calls is on WhiteRoom.
	[BaseType (typeof (NSObject))]
	public interface WhiteDisplayer {
		// Neither SDK watches its own view's bounds, so a rotation or a layout change leaves the
		// drawing surface at the old size until this is called.
		[Export ("refreshViewSize")]
		void RefreshViewSize ();
	}

	// WhiteRoom — WhiteRoom.h. The live session.
	//
	// The completion methods carry [Async] so driving the room is awaitable, but the generator
	// shapes each by its block: a (value, NSError) pair becomes Task<Tuple<value, NSError?>>
	// that always completes — check Item2, non-null means failure — because the generator only
	// applies its fault-on-NSError convention once two or more non-error values remain (verified
	// against the generated code; JoinRoom below is the shape that gets it). ResultTypeName is
	// ignored on these two-parameter blocks and would only orphan the named class, so none is
	// given. The callback overloads remain for consumers who want them.
	[BaseType (typeof (WhiteDisplayer))]
	[DisableDefaultCtor]
	public interface WhiteRoom {
		[Export ("uuid")]
		string Uuid { get; }

		[Export ("phase")]
		WhiteRoomPhase Phase { get; }

		[Export ("memberState", ArgumentSemantic.Strong)]
		WhiteReadonlyMemberState MemberState { get; }

		[Export ("writable")]
		bool Writable { [Bind ("isWritable")] get; }

		[Export ("setMemberState:")]
		void SetMemberState (WhiteMemberState modifyState);

		// SetWritableAsync — Task<Tuple<bool, NSError?>>: the acknowledged writability and the
		// error slot, per the preamble.
		[Export ("setWritable:completionHandler:")]
		[Async]
		void SetWritable (bool writable, [NullAllowed] WhiteWritableHandler completionHandler);

		[Export ("undo")]
		void Undo ();

		[Export ("redo")]
		void Redo ();

		// retainPPT keeps a converted document's own content on the scene and clears only what was
		// drawn over it.
		[Export ("cleanScene:")]
		void CleanScene (bool retainPpt);

		// Scenes are the board's pages, addressed by a path like "/dir/page1".
		// SetScenePathAsync — Task<Tuple<bool, NSError?>>: netless's own success flag plus the
		// error slot, per the preamble.
		[Export ("setScenePath:completionHandler:")]
		[Async]
		void SetScenePath (string dirOrPath, [NullAllowed] WhiteScenePathHandler completionHandler);

		// The block has no parameters, so DisconnectAsync is a plain Task that completes when
		// the room has actually gone.
		[Export ("disconnect:")]
		[Async]
		void Disconnect ([NullAllowed] Action completeHandler);
	}

	// The completion block on -setWritable:completionHandler: — WhiteRoom.h.
	public delegate void WhiteWritableHandler (bool isWritable, [NullAllowed] NSError error);

	// The completion block on -setScenePath:completionHandler: — WhiteRoom.h.
	public delegate void WhiteScenePathHandler (bool success, [NullAllowed] NSError error);

	// The completion block on -joinRoomWithConfig:callbacks:completionHandler: — WhiteSDK+Room.h.
	// On failure room is nil and error is populated.
	public delegate void WhiteJoinRoomHandler (bool success, [NullAllowed] WhiteRoom room, [NullAllowed] NSError error);

	// WhiteRoomCallbackDelegate — WhiteRoomCallbacks.h. Every method is @optional. The undo/redo
	// step counters, magix events and the append-frame error stay unbound.
	[Protocol]
	[Model]
	[BaseType (typeof (NSObject))]
	public interface WhiteRoomCallbackDelegate {
		[Export ("firePhaseChanged:")]
		void FirePhaseChanged (WhiteRoomPhase phase);

		[Export ("fireDisconnectWithError:")]
		void FireDisconnectWithError (string error);

		[Export ("fireKickedWithReason:")]
		void FireKickedWithReason (string reason);

		[Export ("fireCanUndoStepsUpdate:")]
		void FireCanUndoStepsUpdate (nint canUndoSteps);

		[Export ("fireCanRedoStepsUpdate:")]
		void FireCanRedoStepsUpdate (nint canRedoSteps);
	}

	// WhiteCommonCallbackDelegate — WhiteCommonCallbacks.h. SDK-wide rather than per-room; every
	// method is @optional.
	[Protocol]
	[Model]
	[BaseType (typeof (NSObject))]
	public interface WhiteCommonCallbackDelegate {
		[Export ("throwError:")]
		void ThrowError (NSError error);

		[Export ("sdkSetupFail:")]
		void SdkSetupFail (NSError error);
	}

	// WhiteSDK — WhiteSDK.h, plus the (Room) category in WhiteSDK+Room.h. The categories this
	// binding does not expose are (Replayer), for playing a recorded session back, and the
	// PPT/font/app registration surface.
	[BaseType (typeof (NSObject))]
	[DisableDefaultCtor]
	public interface WhiteSDK {
		[Static]
		[Export ("version")]
		string Version { get; }

		[Export ("initWithWhiteBoardView:config:commonCallbackDelegate:")]
		NativeHandle Constructor (WhiteBoardView boardView, WhiteSdkConfiguration config, [NullAllowed] WhiteCommonCallbackDelegate callback);

		// JoinRoomAsync returns Task<WhiteJoinRoomResult> holding the block's two success values
		// (Success, Room) and FAULTS with an NSErrorException on a join failure: with two
		// non-error values left the generator does apply its NSError convention (unlike the
		// room's pair-shaped completions — see the WhiteRoom preamble), and netless populates
		// the error only on genuine failure, so try/catch is the honest shape for a join.
		[Export ("joinRoomWithConfig:callbacks:completionHandler:")]
		[Async (ResultTypeName = "WhiteJoinRoomResult")]
		void JoinRoom (WhiteRoomConfig config, [NullAllowed] WhiteRoomCallbackDelegate callbacks, WhiteJoinRoomHandler completionHandler);
	}
}

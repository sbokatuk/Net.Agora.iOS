//
//  The Objective-C face of netless's Fastboard.
//
//  Fastboard's own public API is already @objc-annotated, so it is *reachable* from Objective-C —
//  but not usefully bindable. A Swift @objc class registers under a module-qualified, mangled
//  runtime name (_TtC9Fastboard8FastRoom), which a .NET binding would have to name literally and
//  which changes with the module name. The @objc(...) attributes below give every type a stable,
//  unmangled name instead, and that is the contract Net.Agora.Fastboard.iOS binds.
//
//  It is deliberately narrower than Fastboard's own surface. What is here is what the
//  cross-platform Net.Agora.Fastboard client needs: create a board with its built-in toolbar, join,
//  draw, undo/redo/clear, go read-only, and hear about the room's lifecycle. Fastboard's overlay
//  customisation, media insertion, pencil-behaviour switches and panel item model stay behind it —
//  they are Swift-shaped (associated values, generics, protocol extensions) and would each need a
//  wrapper of their own.
//
//  The other half of the reason this file exists: joinRoom's real signature takes a
//  Result<WhiteRoom, FastRoomError>, which cannot cross into Objective-C at all. Here it becomes
//  the delegate callbacks below.
//

import Foundation
import UIKit
import Fastboard
import Whiteboard

/// Mirrors Fastboard's own FastRoomPhase, whose raw values it shares.
@objc(AgoraFastboardPhase)
public enum AgoraFastboardPhase: Int {
    case connecting = 0
    case connected = 1
    case reconnecting = 2
    case disconnecting = 3
    case disconnected = 4
    case unknown = 5
}

/// What a board needs to join a room. A flat property bag rather than Fastboard's nested
/// WhiteSdkConfiguration/WhiteRoomConfig pair, because the pair is the whiteboard SDK's shape and
/// a Fastboard caller sets five things.
@objc(AgoraFastboardConfiguration)
public final class AgoraFastboardConfiguration: NSObject {
    @objc public let appIdentifier: String
    @objc public let roomUUID: String
    @objc public let roomToken: String
    @objc public let userUID: String

    /// One of netless's region keys ("cn-hz", "us-sv", "sg", "in-mum", "gb-lon"), or empty for
    /// China, which is Fastboard's own default. A string because that is what the whiteboard SDKs
    /// take on both platforms and what the cross-platform client carries — Fastboard's Region is
    /// an Int enum only it uses.
    @objc public var region: String = ""

    /// Fastboard's Region for this configuration's key.
    var fastRegion: Region {
        switch region {
        case "us-sv": return .US
        case "sg": return .SG
        case "in-mum": return .IN
        case "gb-lon": return .GB
        default: return .CN
        }
    }

    @objc public init(appIdentifier: String, roomUUID: String, roomToken: String, userUID: String) {
        self.appIdentifier = appIdentifier
        self.roomUUID = roomUUID
        self.roomToken = roomToken
        self.userUID = userUID
        super.init()
    }
}

/// The board's lifecycle. Every method is required: an @objc optional protocol member is bound as
/// optional on the .NET side too, and there is no callback here a client can sensibly ignore.
@objc(AgoraFastboardDelegate)
public protocol AgoraFastboardDelegate: NSObjectProtocol {
    func fastboardDidJoin()
    func fastboardDidFail(_ message: String)
    func fastboardWasKicked(_ reason: String)
    func fastboardPhaseDidChange(_ phase: AgoraFastboardPhase)
}

/// A whiteboard with Fastboard's built-in toolbar. Add ``view`` to a view hierarchy, then ``join``.
@objc(AgoraFastboard)
public final class AgoraFastboard: NSObject {
    private let room: FastRoom

    /// The board and its toolbar. Fastboard sizes this itself; give it a frame and it lays out.
    @objc public var view: UIView { room.view }

    @objc public weak var delegate: AgoraFastboardDelegate?

    /// Fastboard's delegate, kept on a private object rather than on this class.
    ///
    /// Not a style choice. With BUILD_LIBRARY_FOR_DISTRIBUTION the compiler emits a textual
    /// .swiftinterface and then verifies it, and a public `extension AgoraFastboard:
    /// FastRoomDelegate` writes Fastboard's types into it as `Fastboard.FastRoom` — which fails to
    /// verify, because the module named Fastboard also contains a *class* named Fastboard, so the
    /// qualified name resolves to the class and "'FastRoom' is not a member type of class
    /// 'Fastboard.Fastboard'". Keeping the conformance private keeps Fastboard's names out of the
    /// public interface entirely, which is what this shim is for anyway.
    private var proxy: DelegateProxy!

    @objc public init(configuration: AgoraFastboardConfiguration) {
        let config = FastRoomConfiguration(
            appIdentifier: configuration.appIdentifier,
            roomUUID: configuration.roomUUID,
            roomToken: configuration.roomToken,
            region: configuration.fastRegion,
            userUID: configuration.userUID)

        room = Fastboard.createFastRoom(withFastRoomConfig: config)
        super.init()

        proxy = DelegateProxy(owner: self)
        room.delegate = proxy
    }

    /// Fastboard reports most join failures to its delegate, but refusing a join because another
    /// room operation is under way, and cancelling one that a disconnect overtook, only reach this
    /// completion. Forwarding exactly those two means the delegate hears each failure once, and a
    /// caller waiting on it isn't left to time out.
    @objc public func join() {
        onMainThread {
            self.room.joinRoom { [weak self] result in
                guard case .failure(let error) = result,
                      error.code == FastRoomErrorType.busy.rawValue
                        || error.code == FastRoomErrorType.operationCancelled.rawValue else { return }
                self?.delegate?.fastboardDidFail(error.localizedDescription)
            }
        }
    }

    @objc public func disconnect() {
        onMainThread { self.room.disconnectRoom() }
    }

    /// The one asynchronous call Fastboard exposes in an Objective-C-compatible shape already; it
    /// is wrapped only to hand back a message rather than an NSError the binding would have to
    /// unpack.
    @objc public func setWritable(_ writable: Bool, completion: ((String?) -> Void)?) {
        onMainThread {
            self.room.updateWritable(writable) { error in
                completion?(error?.localizedDescription)
            }
        }
    }

    /// The drawing tool, as one of the whiteboard SDK's appliance-name strings. Set through the
    /// underlying WhiteRoom rather than Fastboard's panel: the panel is a *view*, and a caller
    /// driving the board from code wants the state changed whether or not the toolbar agrees.
    @objc public func setAppliance(_ applianceName: String, color: [NSNumber]?, strokeWidth: NSNumber?) {
        guard let whiteRoom = room.room else { return }

        let state = WhiteMemberState()
        // WhiteApplianceNameKey is an NS_STRING_ENUM, which Swift imports as a RawRepresentable
        // struct rather than a String — so the name has to be wrapped, not assigned.
        state.currentApplianceName = WhiteApplianceNameKey(rawValue: applianceName)
        if let color { state.strokeColor = color }
        if let strokeWidth { state.strokeWidth = strokeWidth }
        whiteRoom.setMemberState(state)
    }

    @objc public func undo() { room.room?.undo() }

    @objc public func redo() { room.room?.redo() }

    @objc public func clear(_ retainDocument: Bool) { room.room?.cleanScene(retainDocument) }

    @objc public func refreshViewSize() { room.room?.refreshViewSize() }
}

/// Runs `work` on the main thread: straight away if this is it, queued if not.
///
/// Since 1.4.6, Fastboard's joinRoom, disconnectRoom and updateWritable open with a `precondition`
/// on the main thread. A .NET caller is on a thread-pool thread as soon as it has awaited anything,
/// and a failed Swift precondition ends the process with nothing managed code could catch, so the
/// rule stops here rather than becoming every consumer's.
private func onMainThread(_ work: @escaping () -> Void) {
    if Thread.isMainThread {
        work()
    } else {
        DispatchQueue.main.async(execute: work)
    }
}

/// Forwards Fastboard's delegate callbacks to ``AgoraFastboard/delegate``. Private for the reason
/// given on ``AgoraFastboard/proxy``; unowned rather than strong because the board owns it.
private final class DelegateProxy: NSObject, FastRoomDelegate {
    private unowned let owner: AgoraFastboard

    init(owner: AgoraFastboard) {
        self.owner = owner
        super.init()
    }

    func fastboardDidJoinRoomSuccess(_ fastboard: FastRoom, room: WhiteRoom) {
        owner.delegate?.fastboardDidJoin()
    }

    func fastboardDidOccurError(_ fastboard: FastRoom, error: FastRoomError) {
        owner.delegate?.fastboardDidFail(error.localizedDescription)
    }

    func fastboardUserKickedOut(_ fastboard: FastRoom, reason: String) {
        owner.delegate?.fastboardWasKicked(reason)
    }

    func fastboardPhaseDidUpdate(_ fastboard: FastRoom, phase: FastRoomPhase) {
        owner.delegate?.fastboardPhaseDidChange(AgoraFastboardPhase(rawValue: phase.rawValue) ?? .unknown)
    }
}

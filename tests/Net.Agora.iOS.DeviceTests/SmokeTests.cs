using System.Runtime.InteropServices;
#if AGORA_SIGNALING
using Net.Agora.Signaling.iOS;
#elif AGORA_VOICE
using Net.Agora.Voice.iOS;
#else
using Net.Agora.Video.iOS;
using UIKit;
#endif
using ObjCRuntime;

namespace Net.Agora.iOS.DeviceTests;

/// <summary>A single on-simulator check. Throws to fail.</summary>
/// <param name="Name">Human readable name, reported to stdout.</param>
/// <param name="Execute">Runs the check.</param>
public sealed record SmokeTest(string Name, Action Execute);

/// <summary>
/// End-to-end checks that only mean anything on a real device or simulator: they load the native
/// Agora frameworks out of the packaged xcframeworks and drive the raw binding — the RTC
/// flavours' <c>AgoraRtcEngineKit</c> or the Signaling flavour's <c>AgoraRtmClientKit</c>, with
/// no cross-platform façade in between. The façade's own on-device suite is
/// Net.Agora/tests/Net.Agora.DeviceTests; this one exists so a packaging regression is caught in
/// the repository that packs, not two releases later in the one that consumes.
/// </summary>
/// <remarks>
/// Nothing here touches the network. The App ID is syntactically valid but unregistered — the
/// shape of a real one, so the SDK's own client-side format validation does not short-circuit the
/// checks — and no channel is ever joined nor login attempted: those are live signalling calls to
/// Agora's servers, and the façade repository already covers that path with its bounded-timeout
/// join checks. What only this suite proves is that the packed package's native payload loads,
/// its selectors resolve, and the kit round-trips create → drive → destroy.
/// <para>
/// The checks are ordered: the engine has to exist before it can be driven, and a failure early
/// on cascades — which is the intent, since the first failure is the informative one.
/// </para>
/// </remarks>
public static class SmokeTests
{
    // 32 lowercase hex characters — the shape of a real Agora App ID. Same value as the façade
    // repository's SmokeTests, and unregistered on purpose: nothing here needs credentials, and
    // there is nothing to leak if CI logs are public.
    private const string AppId = "0123456789abcdef0123456789abcdef";

    /// <summary>Writes a line to stdout under the current check. Set by the app delegate.</summary>
    public static Action<string> Reporter { get; set; } = _ => { };

#if AGORA_SIGNALING
    /// <summary>The client every check after construction shares. Set by <see cref="ConstructsTheClient"/>.</summary>
    private static AgoraRtmClientKit? _client;

    /// <summary>
    /// Held for the client's lifetime: the kit holds its delegates weakly — see the binding's
    /// AddDelegate comment — so a collected one would silently stop the callbacks.
    /// </summary>
    private static ClientDelegate? _delegate;

    /// <summary>Every check, in the order they must run. Per flavour — see the csproj.</summary>
    public static SmokeTest[] All =>
    [
        new("every native framework is linked and loadable", EveryFrameworkIsLinked),
        new("reports the native SDK version", ReportsTheSdkVersion),
        new("constructs the client with an unregistered App ID", ConstructsTheClient),
        new("an [Async] operation completes end-to-end", AwaitableOperationCompletes),
        new("destroys the client", DestroysTheClient),
    ];
#else
    /// <summary>The engine every check after construction shares. Set by <see cref="ConstructsTheEngine"/>.</summary>
    private static AgoraRtcEngineKit? _engine;

    /// <summary>
    /// Held for the engine's lifetime: the binding hands the delegate to native code, which does
    /// not keep the managed wrapper alive — a collected delegate would turn a later callback into
    /// a crash rather than a failing check.
    /// </summary>
    private static EngineDelegate? _delegate;

    /// <summary>Every check, in the order they must run. Per flavour — see the csproj.</summary>
    public static SmokeTest[] All =>
    [
        new("every native framework is linked and loadable", EveryFrameworkIsLinked),
        new("reports the native SDK version", ReportsTheSdkVersion),
        new("constructs the engine with an unregistered App ID", ConstructsTheEngine),
#if AGORA_VOICE
        new("enables and disables audio", EnablesAndDisablesMedia),
        new("mutes and unmutes the local audio stream", MutesAndUnmutesLocalStreams),
#else
        new("enables and disables video and audio", EnablesAndDisablesMedia),
        new("mutes and unmutes the local streams", MutesAndUnmutesLocalStreams),
        new("attaches and detaches a local video canvas", AttachesALocalVideoCanvas),
        new("switches the camera is reported, not thrown", SwitchesCameraIsReported),
#endif
        new("drives the audio route and volume indication", DrivesAudioRouteAndVolumeIndication),
        new("renews a token without a join is reported, not thrown", RenewTokenIsReported),
        new("sets the channel profile and client role", SetsProfileAndRole),
        new("leave without a join is reported, not thrown", LeaveWithoutAJoinIsReported),
        new("destroys the shared engine", DestroysTheEngine),
    ];
#endif

    private static void Report(string message) => Reporter(message);

    /// <summary>Every framework the package ships. All dynamic — see the csproj.</summary>
    /// <remarks>
    /// For Video, video_dec earns its place on this list twice over: AgoraRtcKit's binary links
    /// <c>@rpath/video_dec.framework/video_dec</c> directly, so a package that dropped it kills
    /// the process at launch — dyld aborts before a single managed frame runs — and nothing at
    /// compile or link time in a consuming app can see it coming. That is exactly the class of
    /// regression this check exists for. The voice build of AgoraRtcKit links no video codec —
    /// the check that it still loads with one fewer framework is the point of the Voice flavour.
    /// Signaling is the two-framework product; the fetch script's slice-stripping makes the same
    /// check worth having.
    /// </remarks>
    private static readonly string[] Frameworks =
    [
#if AGORA_SIGNALING
        "AgoraRtmKit", "aosl",
#elif AGORA_VOICE
        "AgoraRtcKit", "aosl", "Agorafdkaac", "Agoraffmpeg", "AgoraSoundTouch",
#else
        "AgoraRtcKit", "aosl", "Agorafdkaac", "Agoraffmpeg", "AgoraSoundTouch", "video_dec",
#endif
    ];

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern uint _dyld_image_count();

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern IntPtr _dyld_get_image_name(uint index);

    /// <summary>
    /// Proves each of the six xcframeworks actually made it into the app and was loaded.
    /// </summary>
    /// <remarks>
    /// This is the check that catches a packaging regression the compiler cannot see. A binding
    /// assembly reaches its native framework only through selector strings, so a package whose
    /// .resources.zip was empty, or whose xcframework manifest advertised a slice that had been
    /// stripped, still compiles and links — and then fails at runtime the first time a type is
    /// touched. The codec frameworks bind no managed types at all, so for those there is no C#
    /// type whose use would reveal the problem.
    /// </remarks>
    private static void EveryFrameworkIsLinked()
    {
        var images = new List<string>();
        var count = _dyld_image_count();
        for (uint i = 0; i < count; i++)
        {
            var name = Marshal.PtrToStringUTF8(_dyld_get_image_name(i));
            if (name is not null)
            {
                images.Add(name);
            }
        }

        var missing = Frameworks
            .Where(framework => !images.Any(image =>
                image.EndsWith($"/{framework}.framework/{framework}", StringComparison.Ordinal)))
            .ToList();

        Assert(
            missing.Count == 0,
            $"these frameworks were not loaded into the process: {string.Join(", ", missing)}");

        Report($"all {Frameworks.Length} frameworks loaded");
    }

#if AGORA_SIGNALING
    /// <summary>Reads <c>+[AgoraRtmClientKit getVersion]</c> and checks its shape.</summary>
    /// <remarks>
    /// Through the binding rather than a raw selector send, unlike the RTC flavours' version
    /// check: RTM's version getter is part of the bound surface — the thin client reports it —
    /// so the binding call is itself the thing to prove here, not a widening of the package for
    /// the test's sake.
    /// </remarks>
    private static void ReportsTheSdkVersion()
    {
        var version = AgoraRtmClientKit.GetVersion();

        Report($"native SDK version {version ?? "(null)"}");

        // The shape, not a pinned value — same reasoning as the RTC flavours.
        Assert(!string.IsNullOrWhiteSpace(version), "getVersion returned nothing.");
        Assert(
            version!.Any(char.IsAsciiDigit) && version.Contains('.', StringComparison.Ordinal),
            $"'{version}' does not look like a dotted version number.");
    }

    private static void ConstructsTheClient()
    {
        var config = new AgoraRtmClientConfig(AppId, "net-agora-device-tests");

        // The delegate goes in at construction: passing a subclass of the [Model] type is what
        // proves the protocol's exported selectors registered, even though no callback fires in
        // this suite — nothing is subscribed and nobody ever logs in.
        _delegate = new ClientDelegate();
        _client = new AgoraRtmClientKit(config, _delegate, out var error);

        Assert(
            _client.Handle != NativeHandle.Zero,
            $"initWithConfig:delegate:error: rejected the config: {error?.LocalizedDescription}");

        Report("client constructed");
    }

    private static AgoraRtmClientKit Client =>
        _client ?? throw new InvalidOperationException("the client has not been constructed yet.");

    /// <summary>
    /// Awaits <c>LogoutAsync</c> — without a login, so the SDK answers through the completion
    /// block with no network round trip — and asserts the [Async] contract: the Task completes
    /// (never faults) and carries the error as data. This is the one place the generated
    /// awaitable surface is exercised against the real SDK rather than asserted from metadata,
    /// which is what the package tests do.
    /// </summary>
    private static void AwaitableOperationCompletes()
    {
        var task = Client.LogoutAsync();

        // The completion arrives through the main queue and this suite runs on the main thread
        // (see Program.cs), so a blocking Wait would deadlock — pumping the run loop is what
        // lets the completion in while the check stays synchronous.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Foundation.NSRunLoop.Current.RunUntil(Foundation.NSDate.FromTimeIntervalSinceNow(0.05));
        }

        Assert(task.IsCompleted, "LogoutAsync's Task did not complete within 10 seconds.");
        Assert(
            task.IsCompletedSuccessfully,
            $"LogoutAsync's Task is {task.Status} — the binding promises completion, not faulting.");

        var result = task.Result;
        Assert(result is not null, "LogoutAsync completed with a null result.");

        // Which code a login-less logout answers is the SDK's business, not this suite's to
        // pin; that it arrives as data on a completed Task is the binding's contract.
        Report($"logout answered errorCode {result!.ErrorInfo?.ErrorCode.ToString() ?? "(none)"}");
    }

    private static void DestroysTheClient()
    {
        // Destroy is synchronous and the client is unusable afterwards — release everything
        // first, as the binding's own comment on Destroy requires.
        var client = Client;
        _client = null;
        _delegate = null;

        var code = client.Destroy();

        Report($"destroy returned {code}");
    }
#else
    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr object_getClass(IntPtr obj);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool class_respondsToSelector(IntPtr cls, IntPtr sel);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    /// <summary>Reads <c>+[AgoraRtcEngineKit getSdkVersion]</c> and checks its shape.</summary>
    /// <remarks>
    /// Called through the Objective-C runtime rather than the binding, because the binding is
    /// deliberately scoped to what the cross-platform client needs and does not export this
    /// selector — see the ApiDefinition.cs preamble. Widening a shipping package's API surface
    /// just so a test can call it would be backwards; sending the selector by hand proves the
    /// same thing the binding's own calls rely on, that the class is registered and its
    /// selectors resolve against the loaded framework.
    /// </remarks>
    private static void ReportsTheSdkVersion()
    {
        var cls = Class.GetHandle("AgoraRtcEngineKit");
        Assert(cls != IntPtr.Zero, "AgoraRtcEngineKit did not resolve to a native class.");

        var selector = Selector.GetHandle("getSdkVersion");

        // A class method lives on the metaclass, which is what object_getClass returns for a
        // class object. Checked before sending, so a renamed selector fails this assertion
        // rather than raising an unrecognized-selector NSException mid-send.
        Assert(
            class_respondsToSelector(object_getClass(cls), selector),
            "AgoraRtcEngineKit does not respond to +getSdkVersion.");

        var version = Runtime.GetNSObject<Foundation.NSString>(IntPtr_objc_msgSend(cls, selector))?.ToString();

        Report($"native SDK version {version ?? "(null)"}");

        // The shape, not a pinned value: the pod version lives in Directory.Build.props and
        // asserting it here would mean two places to edit on every bump, for a check whose point
        // is only that a real version string came back across the bridge.
        Assert(!string.IsNullOrWhiteSpace(version), "getSdkVersion returned nothing.");
        Assert(
            char.IsAsciiDigit(version![0]) && version.Contains('.', StringComparison.Ordinal),
            $"'{version}' does not look like a dotted version number.");
    }

    private static void ConstructsTheEngine()
    {
        var config = new AgoraRtcEngineConfig
        {
            AppId = AppId,
            ChannelProfile = AgoraChannelProfile.LiveBroadcasting,
        };

        // The delegate goes in at construction: passing a subclass of the [Model] type is what
        // proves the protocol's exported selectors registered, even though no callback fires in
        // this suite — none of the bound callbacks report anything without a join.
        _delegate = new EngineDelegate();
        _engine = AgoraRtcEngineKit.SharedEngine(config, _delegate);

        Assert(_engine is not null, "SharedEngine returned null.");
        Assert(_engine!.Handle != NativeHandle.Zero, "SharedEngine returned an object with no native handle.");

        Report("engine constructed (process-wide singleton, shared by every later check)");
    }

    private static AgoraRtcEngineKit Engine =>
        _engine ?? throw new InvalidOperationException("the engine has not been constructed yet.");

    private static void EnablesAndDisablesMedia()
    {
        // None of these needs camera or microphone permission — enabling a module is not
        // capturing — so zero is the only acceptable answer. StartPreview is deliberately not
        // called in the Video flavour: it is the first call that touches the camera, which on a
        // headless simulator run would raise a TCC permission prompt nobody is there to answer.
#if !AGORA_VOICE
        AssertZero(Engine.EnableVideo(), "enableVideo");
        AssertZero(Engine.DisableVideo(), "disableVideo");
        AssertZero(Engine.EnableVideo(), "enableVideo (again)");
#endif
        AssertZero(Engine.EnableAudio(), "enableAudio");
        AssertZero(Engine.DisableAudio(), "disableAudio");
        AssertZero(Engine.EnableAudio(), "enableAudio (again)");
    }

    private static void MutesAndUnmutesLocalStreams()
    {
        AssertZero(Engine.MuteLocalAudioStream(true), "muteLocalAudioStream(true)");
        AssertZero(Engine.MuteLocalAudioStream(false), "muteLocalAudioStream(false)");
#if !AGORA_VOICE
        AssertZero(Engine.MuteLocalVideoStream(true), "muteLocalVideoStream(true)");
        AssertZero(Engine.MuteLocalVideoStream(false), "muteLocalVideoStream(false)");
#endif
    }

    private static void DrivesAudioRouteAndVolumeIndication()
    {
        // The audio-route setters and the volume-indication cadence started as the Voice
        // binding's additions and are now bound in both flavours, so both prove they cross the
        // bridge. The default-route and volume-indication calls are local engine state and must
        // answer zero.
        // No disabling counterpart is exercised: the header documents interval <= 0 as "disable",
        // but the 4.6.2 engine answers -2 (invalid argument) to it — observed on the first run of
        // this suite — so the claim is the SDK's to sort out, not one to assert either way.
        AssertZero(Engine.SetDefaultAudioRouteToSpeakerphone(true), "setDefaultAudioRouteToSpeakerphone(true)");
        AssertZero(Engine.EnableAudioVolumeIndication(200, 3, reportVad: false), "enableAudioVolumeIndication");

        // The live override is different: on a simulator there is no speaker/earpiece hardware to
        // route between before an audio session starts, and the SDK answers -3 (not ready) — a
        // simulator artifact, observed on the first run of this suite, not a binding defect. Same
        // treatment as LeaveWithoutAJoinIsReported: the value is the SDK's business, what matters
        // is that the call crosses the bridge and comes back as a code rather than a crash.
        var code = Engine.SetEnableSpeakerphone(true);
        Report($"setEnableSpeakerphone(true) returned {code}");
    }

    private static void RenewTokenIsReported()
    {
        // No join ever happens in this suite, so the value is the SDK's business — what matters
        // is that the call crosses the bridge and comes back as a code rather than a crash.
        var code = Engine.RenewToken("0123456789abcdef0123456789abcdef");

        Report($"renewToken returned {code}");
    }

#if !AGORA_VOICE
    private static void SwitchesCameraIsReported()
    {
        // Report-only for the same reason StartPreview is never called here: nothing is
        // capturing on a headless simulator, so the engine legitimately answers an error rather
        // than 0 — crossing the bridge is what this proves.
        var code = Engine.SwitchCamera();

        Report($"switchCamera returned {code}");
    }
#endif

    private static void SetsProfileAndRole()
    {
        AssertZero(Engine.SetChannelProfile(AgoraChannelProfile.LiveBroadcasting), "setChannelProfile");
        AssertZero(Engine.SetClientRole(AgoraClientRole.Broadcaster), "setClientRole(broadcaster)");
        AssertZero(Engine.SetClientRole(AgoraClientRole.Audience), "setClientRole(audience)");
    }

#if !AGORA_VOICE
    private static void AttachesALocalVideoCanvas()
    {
        // SetupLocalVideo adds the SDK's renderer as a subview of whatever UIView is passed —
        // rendering nothing until a preview or join starts, so this is safe without camera
        // permission. The null teardown is the documented way to detach.
        var view = new UIView();

        AssertZero(
            Engine.SetupLocalVideo(new AgoraRtcVideoCanvas { Uid = 0, View = view }),
            "setupLocalVideo");
        AssertZero(Engine.SetupLocalVideo(null), "setupLocalVideo(nil)");
    }
#endif

    private static void LeaveWithoutAJoinIsReported()
    {
        // No join ever happens in this suite, so the value is the SDK's business — what matters
        // is that the call crosses the bridge, including its block argument, and comes back as a
        // code rather than a crash or an exception.
        var code = Engine.LeaveChannel(stats => Report($"leave block fired with {stats}"));

        Report($"leaveChannel returned {code}");
    }

    private static void DestroysTheEngine()
    {
        // Every reference is released before Destroy, as the binding's own comment on Destroy
        // requires — a call into a destroyed engine through a stale wrapper is undefined
        // behaviour, not an error code.
        _engine = null;
        _delegate = null;

        AgoraRtcEngineKit.Destroy();

        Report("destroyed");
    }
#endif

    private static void AssertZero(nint code, string what)
    {
        Assert(code == 0, $"'{what}' returned {code} rather than 0.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

#if AGORA_SIGNALING
    /// <summary>
    /// A subclass of the bound (all-optional) delegate protocol. Its existence at client
    /// construction is the assertion; no callback in the bound surface fires without a login.
    /// </summary>
    private sealed class ClientDelegate : AgoraRtmClientDelegate
    {
        public override void ConnectionChangedToState(
            AgoraRtmClientKit rtmKit, string channelName, AgoraRtmClientConnectionState state, nint reason) =>
            SmokeTests.Reporter($"delegate: connection {state}");
    }
#else
    /// <summary>
    /// A subclass of the bound (all-optional) delegate protocol. Its existence at engine
    /// construction is the assertion; no callback in the bound surface fires without a join.
    /// </summary>
    private sealed class EngineDelegate : AgoraRtcEngineDelegate
    {
        public override void DidOccurError(AgoraRtcEngineKit engine, AgoraErrorCode errorCode) =>
            SmokeTests.Reporter($"delegate: error {errorCode}");
    }
#endif
}

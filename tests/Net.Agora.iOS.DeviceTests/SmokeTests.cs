using System.Runtime.InteropServices;
#if AGORA_FASTBOARD
using Net.Agora.Fastboard.iOS;
#elif AGORA_WHITEBOARD
using Net.Agora.Whiteboard.iOS;
#elif AGORA_CHAT
using Net.Agora.Chat.iOS;
#elif AGORA_SIGNALING
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
/// Agora payload out of the packaged xcframeworks and drive the raw binding — the RTC flavours'
/// <c>AgoraRtcEngineKit</c>, the Signaling flavour's <c>AgoraRtmClientKit</c>, Chat's
/// <c>AgoraChatClient</c> or the two board flavours' netless classes — with no cross-platform
/// façade in between. The façade's own on-device suite is Net.Agora/tests/Net.Agora.DeviceTests;
/// this one exists so a packaging regression is caught in the repository that packs, not two
/// releases later in the one that consumes.
/// </summary>
/// <remarks>
/// Nothing here touches the network. The App ID is syntactically valid but unregistered — the
/// shape of a real one, so the SDK's own client-side format validation does not short-circuit the
/// checks — and no channel is ever joined, no login attempted and no whiteboard room entered:
/// those are live signalling calls to Agora's and netless's servers, and the façade repository
/// already covers that path with its bounded-timeout join checks. What only this suite proves is
/// that the packed package's native payload loads, its selectors resolve, and the kit round-trips
/// create → drive → destroy.
/// <para>
/// One product per app. The RTC pair collide on a framework name; Whiteboard and Fastboard collide
/// on netless's static library and on Whiteboard_Whiteboard.bundle, which they both declare. See
/// the .csproj.
/// </para>
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

#if AGORA_FASTBOARD
    /// <summary>Kept for the app's lifetime so the construction check's object is not collected mid-suite.</summary>
    private static AgoraFastboardConfiguration? _configuration;

    /// <summary>Kept for the app's lifetime for the same reason: its view hierarchy outlives the check.</summary>
    private static AgoraFastboard? _board;

    /// <summary>Held for the board's lifetime: the shim's delegate is weak, as the engine's is above.</summary>
    private static BoardFailures? _failures;

    /// <summary>Every check, in the order they must run. Per flavour — see the csproj.</summary>
    public static SmokeTest[] All =>
    [
        new("every class of the static library is registered", EveryClassIsRegistered),
        new("reports the embedded whiteboard SDK version", ReportsTheSdkVersion),
        new("both resource bundles reached the app bundle and load", ResourceBundlesAreLoadable),
        new("constructs the configuration", ConstructsTheClient),
        new("constructs a board and drives it off the main thread", DrivesTheBoardOffTheMainThread),
        new("the error messages' bundle resolves the way Fastboard looks it up", ErrorMessagesAreLocalised),
        new("reports a refused join and a failed one through the delegate", ReportsJoinFailures),
    ];
#elif AGORA_WHITEBOARD
    /// <summary>Kept for the app's lifetime so the construction check's objects are not collected mid-suite.</summary>
    private static WhiteSdkConfiguration? _configuration;

    /// <summary>The board view the construction check creates. Held for the same reason.</summary>
    private static WhiteBoardView? _boardView;

    /// <summary>Every check, in the order they must run. Per flavour — see the csproj.</summary>
    public static SmokeTest[] All =>
    [
        new("every class of the static library is registered", EveryClassIsRegistered),
        new("reports the native SDK version", ReportsTheSdkVersion),
        new("the board's resource bundle reached the app bundle and loads", ResourceBundlesAreLoadable),
        new("constructs the SDK configuration and a board view", ConstructsTheClient),
    ];
#elif AGORA_CHAT
    /// <summary>The client every check after construction shares. Set by <see cref="ConstructsTheClient"/>.</summary>
    private static AgoraChatClient? _client;

    /// <summary>
    /// Held for the client's lifetime: -addDelegate:delegateQueue: keeps its delegates weakly, as
    /// every Agora kit here does, so a collected one would silently stop the callbacks.
    /// </summary>
    private static ClientDelegate? _delegate;

    /// <summary>Every check, in the order they must run. Per flavour — see the csproj.</summary>
    public static SmokeTest[] All =>
    [
        new("every native framework is linked and loadable", EveryFrameworkIsLinked),
        new("reports the native SDK version", ReportsTheSdkVersion),
        new("constructs the client with an unregistered app key", ConstructsTheClient),
        new("an [Async] operation completes end-to-end", AwaitableOperationCompletes),
    ];
#elif AGORA_SIGNALING
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

#if AGORA_WHITEBOARD || AGORA_FASTBOARD
    /// <summary>Classes the static library must have contributed to the app's own binary.</summary>
    /// <remarks>
    /// The board flavours get this where every other flavour gets the dyld image list, and the
    /// difference is not cosmetic: netless ships a <em>static</em> library, so there is no
    /// <c>X.framework/X</c> image for dyld to report — the code is linked into the app's own
    /// executable, which dyld names once, as the app. The regression to catch is the same shape
    /// though, and if anything likelier here: a static library only contributes the translation
    /// units the linker decides it needs, which is why the binding passes <c>ForceLoad</c> (see
    /// the NativeReference block in the binding .csproj), and a unit dropped anyway surfaces as a
    /// nil class or an unrecognised selector the first time a type is touched — never as a link
    /// error. So the equivalent proof is that the Objective-C runtime knows the classes by name.
    /// <para>
    /// The SwiftPM module-bundle finder is on the list deliberately: it is the class that performs
    /// the lookup <see cref="ResourceBundlesAreLoadable"/> asserts, so the two checks together say
    /// the finder is present *and* has something to find.
    /// </para>
    /// </remarks>
    private static readonly string[] Classes =
    [
#if AGORA_FASTBOARD
        // The shim's own types — see native/AgoraFastboardBridge, whose @objc(...) names these
        // are. The White* pair below come from netless's whiteboard library, which Fastboard's
        // static library contains outright: that is why Fastboard and Whiteboard cannot be
        // referenced by one app, and asserting them here is what keeps that claim honest.
        "AgoraFastboard", "AgoraFastboardConfiguration",
        "WhiteSDK", "WhiteSdkConfiguration",
        "Fastboard_FastboardDynamic_SWIFTPM_MODULE_BUNDLER_FINDER",
        "Whiteboard_Whiteboard_SWIFTPM_MODULE_BUNDLER_FINDER",
#else
        "WhiteSDK", "WhiteSdkConfiguration", "WhiteBoardView",
        "WhiteRoom", "WhiteRoomConfig", "WhiteDisplayer", "WhiteMemberState",
        "Whiteboard_Whiteboard_SWIFTPM_MODULE_BUNDLER_FINDER",
#endif
    ];

    private static void EveryClassIsRegistered()
    {
        var missing = Classes.Where(name => Class.GetHandle(name) == IntPtr.Zero).ToList();

        Assert(
            missing.Count == 0,
            $"these classes are not registered in the process: {string.Join(", ", missing)}");

        // The same question asked through a bound type rather than a literal string, which is what
        // a consuming app actually depends on: this overload resolves the [Register] name the
        // generator gave the binding, so it fails if the binding and the library ever disagree
        // about what a class is called — something a list of literals here could not notice.
#if AGORA_FASTBOARD
        Assert(
            Class.GetHandle(typeof(AgoraFastboard)) != IntPtr.Zero,
            "the AgoraFastboard binding's registered class name does not resolve.");
#else
        Assert(
            Class.GetHandle(typeof(WhiteSDK)) != IntPtr.Zero,
            "the WhiteSDK binding's registered class name does not resolve.");
#endif

        Report($"all {Classes.Length} classes registered");
    }

    /// <summary>
    /// Every resource bundle the package must have staged into the app bundle, and one file that
    /// has to be inside each — an empty directory with an Info.plist would otherwise pass.
    /// </summary>
    private static readonly (string Name, string Witness, string WitnessType)[] ResourceBundles =
    [
#if AGORA_FASTBOARD
        // Fastboard's own: the toolbar's icons, as a compiled asset catalogue, plus its localised
        // strings. Missing, the toolbar comes up as a row of blank buttons.
        ("Fastboard_Fastboard", "Assets", "car"),
#endif
        // The whiteboard SDK's: the web page the board actually is. Fastboard ships this one too,
        // since it is a UI layer over the same SDK.
        ("Whiteboard_Whiteboard", "index", "html"),
    ];

    /// <summary>
    /// Proves the resource bundles reached the app bundle under the exact names the libraries look
    /// them up by, and that each opens as a bundle with its payload intact.
    /// </summary>
    /// <remarks>
    /// The highest-value check in the board flavours, because it is the only one that can catch
    /// this failure at all. The board is a web app: without Whiteboard_Whiteboard.bundle the view
    /// comes up blank and *nothing is reported* — no exception, no error callback, no log line —
    /// which is the single most confusing way either package can fail. Fastboard's own bundle
    /// fails just as quietly, as a toolbar of blank buttons.
    /// <para>
    /// The name is not ours and cannot be tidied. Each library finds its bundle through the
    /// module-bundle finder Swift Package Manager generated when it was built, which composes
    /// "&lt;module&gt;.bundle" from a "%@.bundle" format string — both are visible with
    /// <c>strings</c> on the shipped .a — and asks the main bundle for it. So this check asks the
    /// same question the same way: by that literal name, of NSBundle.MainBundle.
    /// </para>
    /// <para>
    /// It is also the only check in either repository that exercises the delivery mechanism end to
    /// end. A binding project cannot ship a @(BundleResource) of its own, so the bundle is packed
    /// as plain content and re-declared in the consuming app by the .targets file NuGet imports
    /// from buildTransitive/ — and this app is a consuming app. The package tests assert the
    /// bundle and the .targets are *in the .nupkg*; only running it can show the .targets actually
    /// fired and put the files somewhere NSBundle will find them.
    /// </para>
    /// <para>
    /// Confirmed to have teeth rather than assumed: deleting the bundle from a built .app and
    /// re-running turns this check red with the message below, and takes the construction check
    /// after it down too.
    /// </para>
    /// </remarks>
    private static void ResourceBundlesAreLoadable()
    {
        foreach (var (name, witness, witnessType) in ResourceBundles)
        {
            var path = Foundation.NSBundle.MainBundle.PathForResource(name, "bundle");

            Assert(
                !string.IsNullOrEmpty(path),
                $"{name}.bundle is not in the app bundle. The .targets file shipped in "
                    + "buildTransitive/ is what declares it as a BundleResource of the consuming "
                    + "app — see the binding .csproj.");

            var bundle = Foundation.NSBundle.FromPath(path);

            Assert(bundle is not null, $"{name}.bundle did not open as a bundle.");

            // Its own Info.plist, parsed by CoreFoundation rather than read as a file: a directory
            // that merely exists is not a loadable bundle.
            Assert(
                !string.IsNullOrEmpty(bundle.BundleIdentifier),
                $"{name}.bundle opened but has no bundle identifier — its Info.plist did not survive packing.");

            var resource = bundle.PathForResource(witness, witnessType);

            Assert(
                !string.IsNullOrEmpty(resource),
                $"{name}.bundle is present but {witness}.{witnessType} is not in it.");

            Report($"{name}.bundle loaded ({bundle.BundleIdentifier}), {witness}.{witnessType} present");
        }
    }
#else
    /// <summary>Every framework the package ships. All dynamic — see the csproj.</summary>
    /// <remarks>
    /// For Video, video_dec earns its place on this list twice over: AgoraRtcKit's binary links
    /// <c>@rpath/video_dec.framework/video_dec</c> directly, so a package that dropped it kills
    /// the process at launch — dyld aborts before a single managed frame runs — and nothing at
    /// compile or link time in a consuming app can see it coming. That is exactly the class of
    /// regression this check exists for. The voice build of AgoraRtcKit links no video codec —
    /// the check that it still loads with one fewer framework is the point of the Voice flavour.
    /// Signaling is the two-framework product; the fetch script's slice-stripping makes the same
    /// check worth having. Chat is the other two-framework product, and the only one whose aosl
    /// comes from a second archive — AgoraInfra_iOS, which its pod merely declares a dependency on
    /// — so a fetch script that downloaded one and not the other fails exactly here.
    /// </remarks>
    private static readonly string[] Frameworks =
    [
#if AGORA_CHAT
        "AgoraChat", "aosl",
#elif AGORA_SIGNALING
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
#endif

    // Raw Objective-C messaging, for the flavours that have to send a selector their binding
    // deliberately does not export: the RTC pair's +getSdkVersion, and Fastboard's
    // +[WhiteSDK version], which belongs to the whiteboard library Fastboard embeds rather than to
    // the shim this package binds. Declared once, here, rather than per flavour.
#if !AGORA_SIGNALING && !AGORA_CHAT && !AGORA_WHITEBOARD
    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr object_getClass(IntPtr obj);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool class_respondsToSelector(IntPtr cls, IntPtr sel);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    /// <summary>Sends a zero-argument class selector returning an NSString, or fails the check.</summary>
    private static string? ClassStringSelector(string className, string selectorName)
    {
        var cls = Class.GetHandle(className);
        Assert(cls != IntPtr.Zero, $"{className} did not resolve to a native class.");

        var selector = Selector.GetHandle(selectorName);

        // A class method lives on the metaclass, which is what object_getClass returns for a
        // class object. Checked before sending, so a renamed selector fails this assertion
        // rather than raising an unrecognized-selector NSException mid-send.
        Assert(
            class_respondsToSelector(object_getClass(cls), selector),
            $"{className} does not respond to +{selectorName}.");

        return Runtime.GetNSObject<Foundation.NSString>(IntPtr_objc_msgSend(cls, selector))?.ToString();
    }
#endif

    /// <summary>Checks a version string's shape, not its value.</summary>
    /// <remarks>
    /// The shape, never a pinned value: the pod version lives in Directory.Build.props and
    /// asserting it here would mean two places to edit on every bump, for a check whose point is
    /// only that a real version string came back across the bridge.
    /// </remarks>
    private static void AssertLooksLikeAVersion(string? version)
    {
        Assert(!string.IsNullOrWhiteSpace(version), "the version getter returned nothing.");
        Assert(
            version.Any(char.IsAsciiDigit) && version.Contains('.', StringComparison.Ordinal),
            $"'{version}' does not look like a dotted version number.");
    }

#if AGORA_FASTBOARD
    // Placeholders of the right shape, registered nowhere. The configuration validates none of
    // them and nothing here joins a room, so they never leave the process.
    private const string RoomUuid = "0123456789abcdef0123456789abcdef";
    private const string RoomToken = "NETLESSROOM_0123456789abcdef";

    /// <summary>Reads <c>+[WhiteSDK version]</c> through the Objective-C runtime.</summary>
    /// <remarks>
    /// Through the runtime rather than the binding, and deliberately of a class this package does
    /// not bind at all. WhiteSDK belongs to netless's whiteboard library, which Fastboard's static
    /// library contains but Net.Agora.Fastboard.iOS does not expose — what it exposes is the shim
    /// (see native/AgoraFastboardBridge) — and widening a shipping package's API surface so a test
    /// can call into it would be backwards. Sending the selector by hand proves the thing this
    /// flavour needs proved and the class list above cannot: the embedded whiteboard code is live
    /// in the process, not merely present as symbols in the binary.
    /// </remarks>
    private static void ReportsTheSdkVersion()
    {
        var version = ClassStringSelector("WhiteSDK", "version");

        Report($"embedded whiteboard SDK version {version ?? "(null)"}");

        AssertLooksLikeAVersion(version);
    }

    private static void ConstructsTheClient()
    {
        // A flat property bag that crosses the same bridge as the board and answers offline. The
        // board itself is the next check. Joining a real room is not covered here at all: the
        // façade repository's suite drives that, with a bounded timeout against a real endpoint.
        _configuration = new AgoraFastboardConfiguration(AppId, RoomUuid, RoomToken, "net-agora-device-tests")
        {
            Region = "us-sv",
        };

        Assert(
            _configuration.Handle != NativeHandle.Zero,
            "initWithAppIdentifier:roomUUID:roomToken:userUID: returned no native handle.");

        // Read back, not just written: these are the shim's own @objc properties, and a round trip
        // is what shows the four arguments arrived in the order the binding declares them.
        Assert(_configuration.AppIdentifier == AppId, "the app identifier did not round-trip.");
        Assert(_configuration.RoomUuid == RoomUuid, "the room UUID did not round-trip.");
        Assert(_configuration.RoomToken == RoomToken, "the room token did not round-trip.");
        Assert(_configuration.Region == "us-sv", "the region did not round-trip.");

        Report("configuration constructed");
    }

    /// <summary>
    /// Calls two of the shim's methods from a thread-pool thread, where a .NET caller ends up after
    /// any <c>await</c>.
    /// </summary>
    /// <remarks>
    /// Fastboard's <c>disconnectRoom</c> and <c>updateWritable</c> open with a Swift
    /// <c>precondition</c> on the main thread, so without the shim's hop this check ends the process
    /// and the verdict line never prints. Nothing here reaches the network: outside a room the first
    /// returns at once, and the second answers "not ready" through its completion block, which is
    /// also what shows the call really ran on the far side of the hop.
    /// </remarks>
    private static void DrivesTheBoardOffTheMainThread()
    {
        var board = new AgoraFastboard(_configuration!);
        _board = board;

        var task = Task.Run(() =>
        {
            var answer = new TaskCompletionSource<string?>();
            board.Disconnect();
            board.SetWritable(true, answer.SetResult);
            return answer.Task;
        });

        // The hop lands on the main queue and this suite runs on the main thread (see Program.cs),
        // so a blocking Wait would deadlock, exactly as in the Chat flavour's awaitable check.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Foundation.NSRunLoop.Current.RunUntil(Foundation.NSDate.FromTimeIntervalSinceNow(0.05));
        }

        Assert(task.IsCompleted, "SetWritable's completion did not arrive within 10 seconds.");
        Assert(task.IsCompletedSuccessfully, $"driving the board off the main thread ended {task.Status}.");
        Assert(task.Result is not null, "SetWritable reported success outside a room.");

        Report($"outside a room, SetWritable answered: {task.Result}");
    }

    /// <summary>
    /// Follows Fastboard's own lookup: a bundle named LocalizedStrings inside its module bundle,
    /// which SwiftPM doesn't produce and build-fastboard.sh does.
    /// </summary>
    private static void ErrorMessagesAreLocalised()
    {
        var module = Foundation.NSBundle.FromPath(Foundation.NSBundle.MainBundle.PathForResource("Fastboard_Fastboard", "bundle"));
        Assert(module is not null, "Fastboard_Fastboard.bundle did not open.");

        var path = module.PathForResource("LocalizedStrings", "bundle");
        Assert(!string.IsNullOrEmpty(path), "LocalizedStrings.bundle is not inside Fastboard_Fastboard.bundle.");

        var message = Foundation.NSBundle.FromPath(path)?.GetLocalizedString("joinRoomError", null, null);
        Assert(message == "Join room error", $"joinRoomError resolved to '{message}'.");

        Report($"joinRoomError resolves to '{message}'");
    }

    /// <summary>
    /// Fastboard answers two join failures only through a completion block the Objective-C surface
    /// can't carry, so without the shim's forwarding these never reach the delegate, and a caller
    /// waiting on it times out. The second join here is the refused one.
    /// </summary>
    /// <remarks>
    /// The first join is left to fail on its own, for the end-to-end half of the localisation
    /// check. The placeholders can't enter a room, and which stage turns them down is the SDK's
    /// business, so either sentence will do; what must not come back is the key behind it. It is
    /// the one check in this flavour that starts a join, so it stays bounded.
    /// </remarks>
    private static void ReportsJoinFailures()
    {
        var failures = new BoardFailures();
        _failures = failures;
        _board!.Delegate = failures;

        _board.Join();
        _board.Join();

        Assert(failures.Messages.Count == 1, $"a refused join reported {failures.Messages.Count} failures, not 1.");
        Report($"refused: {failures.Messages[0]}");

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (failures.Messages.Count < 2 && DateTime.UtcNow < deadline)
        {
            Foundation.NSRunLoop.Current.RunUntil(Foundation.NSDate.FromTimeIntervalSinceNow(0.05));
        }

        Assert(failures.Messages.Count == 2, "the join with placeholders did not fail within 15 seconds.");
        Assert(
            failures.Messages[1] is "Join room error" or "Error on setupSDK",
            $"the failed join reported '{failures.Messages[1]}'.");
        Report($"failed: {failures.Messages[1]}");
    }

    /// <summary>Collects what the board reports. Every member is required by the protocol.</summary>
    private sealed class BoardFailures : AgoraFastboardDelegate
    {
        public List<string> Messages { get; } = [];

        public override void FastboardDidJoin() => Messages.Add("joined with placeholders");

        public override void FastboardDidFail(string message) => Messages.Add(message);

        public override void FastboardWasKicked(string reason) => Messages.Add($"kicked: {reason}");

        public override void FastboardPhaseDidChange(AgoraFastboardPhase phase)
        {
        }
    }
#elif AGORA_WHITEBOARD
    /// <summary>Reads <c>+[WhiteSDK version]</c> through the binding's static property.</summary>
    /// <remarks>
    /// Through the binding rather than a raw selector send, unlike the RTC and Fastboard flavours:
    /// netless's version getter is part of the bound surface here, so the binding call is itself
    /// the thing to prove. It is also the first call in this flavour that reaches into the
    /// force-loaded static library, which is a step the class lookups above cannot take on their
    /// own — a registered class is not yet a class whose methods survived the link.
    /// </remarks>
    private static void ReportsTheSdkVersion()
    {
        var version = WhiteSDK.Version;

        Report($"native SDK version {version ?? "(null)"}");

        AssertLooksLikeAVersion(version);
    }

    private static void ConstructsTheClient()
    {
        _configuration = new WhiteSdkConfiguration(AppId)
        {
            // One of netless's data-centre keys. Nothing resolves it without a join.
            Region = "us-sv",
            Log = false,
            UseMultiViews = false,
        };

        Assert(_configuration.Handle != NativeHandle.Zero, "initWithApp: returned no native handle.");
        Assert(_configuration.AppIdentifier == AppId, "the app identifier did not round-trip.");
        Assert(_configuration.Region == "us-sv", "the region did not round-trip.");

        // The board view too, but no WhiteSDK over it and no room ever joined. -init is already
        // where netless's view loads its initial resource, and that resource is the local bundle:
        // -loadInitialResource -> -resourceURL -> +[NSURL fileURLWithPath:], file:// throughout.
        // Nothing reaches netless until a room is joined, which is the step this suite stops
        // short of.
        //
        // So this doubles as a second, independent reading of the bundle check above. Delete
        // Whiteboard_Whiteboard.bundle from a built .app and this line throws
        // NSInvalidArgumentException out of +[NSURL fileURLWithPath:] on a nil path — verified,
        // by doing exactly that. Which is why the explicit NSBundle lookup runs first: the same
        // defect, named, rather than an Objective-C exception three frames inside the SDK.
        _boardView = new WhiteBoardView();

        Assert(_boardView.Handle != NativeHandle.Zero, "WhiteBoardView's -init returned no native handle.");

        Report("configuration and board view constructed");
    }
#elif AGORA_CHAT
    // The app key's shape is {org}#{app} — Easemob's scheme, which Agora kept when it took the
    // product over. Unregistered, like the App ID above and for the same reasons.
    private const string AppKey = "netagora#devicetests";

    /// <summary>Reads <c>-[AgoraChatClient version]</c> off the singleton.</summary>
    /// <remarks>
    /// Through the binding rather than a raw selector send, like Signaling's and unlike the RTC
    /// flavours': Chat's version getter is part of the bound surface. It runs before the SDK is
    /// initialised on purpose — +sharedClient has to hand back a usable object first, and this is
    /// the cheapest call that proves it did.
    /// </remarks>
    private static void ReportsTheSdkVersion()
    {
        var version = AgoraChatClient.SharedClient.Version;

        Report($"native SDK version {version ?? "(null)"}");

        AssertLooksLikeAVersion(version);
    }

    private static void ConstructsTheClient()
    {
        var options = AgoraChatOptions.FromAppKey(AppKey);

        Assert(options is not null, "optionsWithAppkey: returned null.");
        Assert(options.AppKey == AppKey, "the app key did not round-trip.");

        // Auto-login off, explicitly. It is the one switch in this class that would send the SDK
        // to the network unprompted, and there is no credential in this process for it to use.
        options.IsAutoLogin = false;
        options.EnableConsoleLog = false;
        options.LogLevel = AgoraChatLogLevel.Error;

        _client = AgoraChatClient.SharedClient;

        // Synchronous, and nil on success — the SDK's convention for its non-block calls, see the
        // binding. It sets up local storage; it does not connect.
        var error = _client.InitializeSdk(options);

        Assert(
            error is null,
            $"initializeSDKWithOptions: failed with {error?.Code}: {error?.ErrorDescription}");

        // The delegate goes in after initialisation: passing a subclass of the [Model] type is
        // what proves the protocol's exported selectors registered, even though no callback fires
        // in this suite — nothing here ever connects. A null queue means the main one.
        _delegate = new ClientDelegate();
        _client.AddDelegate(_delegate, null);

        Assert(!_client.IsLoggedIn, "a freshly initialised client reports itself logged in.");

        Report($"client constructed and SDK initialised for app key {_client.Options.AppKey}");
    }

    private static AgoraChatClient Client =>
        _client ?? throw new InvalidOperationException("the client has not been constructed yet.");

    /// <summary>
    /// Awaits <c>LogoutAsync</c> — without a login, so the SDK answers through the completion
    /// block with no network round trip — and asserts the [Async] contract: the Task completes
    /// (never faults) and carries the error as data. This is the one place Chat's generated
    /// awaitable surface is exercised against the real SDK rather than asserted from metadata,
    /// which is what the package tests do.
    /// </summary>
    /// <remarks>
    /// Chat's completion-not-fault contract is stronger than Signaling's, and holds for a
    /// different reason: AgoraChatError is not an NSError, so the generator has no error type to
    /// fault with and hands the whole callback value back as the result. A null result is success
    /// everywhere in this SDK. See the ApiDefinition's comment on these three overloads.
    /// </remarks>
    private static void AwaitableOperationCompletes()
    {
        // unbindDeviceToken: false. True is the branch that would need the server, to tell it to
        // stop pushing to a device token nothing here ever registered.
        var task = Client.LogoutAsync(false);

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

        // Whether a login-less logout answers an error or a bare success is the SDK's business,
        // not this suite's to pin; that it answers at all, as data on a completed Task, is the
        // binding's contract.
        Report(task.Result is { } error
            ? $"logout answered code {error.Code}: {error.ErrorDescription}"
            : "logout answered success (null error)");
    }
#elif AGORA_SIGNALING
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

        AssertLooksLikeAVersion(version);
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
        var version = ClassStringSelector("AgoraRtcEngineKit", "getSdkVersion");

        Report($"native SDK version {version ?? "(null)"}");

        AssertLooksLikeAVersion(version);
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

    /// <summary>Fails the current check unless <paramref name="condition"/> holds.</summary>
    /// <remarks>
    /// The [DoesNotReturnIf] annotation is not decoration: without it the compiler cannot see that
    /// a null check asserted here holds afterwards, and every follow-up use of the value needs a
    /// null-forgiving <c>!</c> — which suppresses the next genuine one just as effectively.
    /// </remarks>
    private static void Assert(
        [System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

#if AGORA_WHITEBOARD || AGORA_FASTBOARD
    // No delegate type in the board flavours. Both SDKs deliver every callback they have only
    // after a room join, which this suite never performs, so a subclass would assert nothing the
    // class-registration check above does not already cover.
#elif AGORA_CHAT
    /// <summary>
    /// A subclass of the bound (all-optional) delegate protocol. Its existence at client
    /// construction is the assertion; no callback in the bound surface fires without a connection.
    /// </summary>
    private sealed class ClientDelegate : AgoraChatClientDelegate
    {
        public override void ConnectionStateDidChange(AgoraChatConnectionState state) =>
            SmokeTests.Reporter($"delegate: connection {state}");
    }
#elif AGORA_SIGNALING
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

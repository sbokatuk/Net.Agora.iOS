using System.IO.Compression;

namespace Net.Agora.iOS.PackageTests;

/// <summary>
/// Four of the packages here ship an <c>aosl.xcframework</c> — Agora's own infrastructure library,
/// which AgoraRtcKit, AgoraRtmKit, AgoraChat and eleven of the thirteen extension frameworks link
/// as an <c>@rpath</c> dependency. In an app that references more than one of those packages they
/// do not each get their own: every copy unpacks to <c>Frameworks/aosl.framework</c>, the bundle
/// carries exactly one, and which one is whichever the build happened to keep. So every product in
/// that app runs against a library it may not have been built against.
/// </summary>
/// <remarks>
/// <para>
/// That is not hypothetical. Up to <c>Net.Agora.Signaling.iOS</c> 2.2.6.2 the RTM zip's bundled
/// aosl was 1.2.13 where the RTC pods bundle 1.3.5, and 1.2.13 does not export
/// <c>aosl_ref_magic</c>. An app referencing Signaling and Video could therefore fail to load
/// AgoraRtcKit at all — decided by nothing the developer controls, and reported by nothing in the
/// build. The Android bindings shipped the identical bug through a different mechanism
/// (agora-rtm's vendored <c>libaosl.so</c>) and it reached a device before anyone saw it.
/// </para>
/// <para>
/// The invariant is therefore not that the copies are identical — they are deliberately not, since
/// Chat pins <c>AgoraInfra_iOS</c> 1.3.9 while the RTC pods and Signaling are on 1.3.5, and
/// forcing lockstep between unrelated products would buy nothing. It is that <em>whichever copy
/// wins, all of them still load</em>: every aosl symbol any framework here imports must be
/// exported by every aosl any package here ships.
/// </para>
/// <para>
/// This reads real Mach-O symbol tables rather than comparing version strings, because a version
/// string can agree while the exports do not — 1.3.9 both adds three symbols and drops two
/// relative to 1.3.5. See <see cref="MachO"/> for why the parsing is done in-process rather than
/// by shelling out to <c>nm</c>.
/// </para>
/// </remarks>
public class AoslCompatibilityTests
{
    private const string PayloadTargetFramework = "net8.0-ios18.0";

    /// <summary>
    /// The device slice. One architecture, and the one that ships to users; a framework's imports
    /// and exports do not differ between its slices.
    /// </summary>
    private const string DeviceSlice = "ios-arm64";

    /// <summary>Every package that ships an aosl, and the SDK framework in it that links one.</summary>
    private static readonly (string PackageId, string Framework)[] Suppliers =
    [
        (Packages.Video, "AgoraRtcKit"),
        (Packages.Voice, "AgoraRtcKit"),
        (Packages.Signaling, "AgoraRtmKit"),
        (Packages.Chat, "AgoraChat"),
    ];

    [Fact]
    public void Every_shipped_aosl_satisfies_every_framework_that_links_one()
    {
        // What the frameworks ask aosl for, pooled: any one of them may be the one left holding
        // another package's copy, so each copy has to answer for all of them at once.
        var required = new SortedSet<string>(StringComparer.Ordinal);
        var exportsByPackage = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var (packageId, framework) in Suppliers)
        {
            var exports = MachO.Exports(Binary(packageId, "aosl"));
            exportsByPackage[packageId] = exports;

            // Intersected with an aosl's exports rather than taken whole: a framework's undefined
            // symbols also cover libSystem, libc++ and the Objective-C runtime, and only the ones
            // aosl actually provides are aosl's problem.
            required.UnionWith(MachO.Undefined(Binary(packageId, framework)).Where(exports.Contains));
        }

        // The extension packages ship no aosl of their own — they are native payload only — but
        // they link one, so they are consumers of whichever copy the app kept just as much as the
        // SDKs are. Eleven of the thirteen frameworks import aosl symbols, two of them eleven
        // apiece. Left out, this check would pass on an aosl that satisfies every SDK and breaks
        // an extension, which is a failure mode with even less to show for itself: an extension
        // that cannot load reports a refused switch, not a crash.
        var reference = exportsByPackage[Packages.Video];
        foreach (var (packageId, frameworks) in Packages.Extensions)
        {
            foreach (var framework in frameworks)
            {
                required.UnionWith(MachO.Undefined(Binary(packageId, framework)).Where(reference.Contains));
            }
        }

        Assert.True(required.Count > 0, "No aosl symbols were resolved at all — the payload layout must have changed.");

        var failures = exportsByPackage
            .Select(entry => (entry.Key, Missing: required.Except(entry.Value).ToList()))
            .Where(result => result.Missing.Count > 0)
            .Select(result =>
                $"{result.Key} ships an aosl missing {result.Missing.Count} symbol(s) another " +
                $"framework here imports: {string.Join(", ", result.Missing.Take(5))}")
            .ToList();

        Assert.True(
            failures.Count == 0,
            "An app referencing more than one of these packages keeps a single aosl.framework, so " +
            "every shipped copy has to satisfy every SDK. See build/fetch-signaling.sh.\n" +
            string.Join("\n", failures));
    }

    /// <summary>One framework's device-slice Mach-O, out of a package's native payload.</summary>
    private static byte[] Binary(string packageId, string framework)
    {
        using var package = Packages.OpenPackage(packageId);
        using var payload = Packages.OpenNativePayload(package, packageId, PayloadTargetFramework);

        var path = $"{framework}.xcframework/{DeviceSlice}/{framework}.framework/{framework}";
        var entry = payload.GetEntry(path);
        Assert.True(entry is not null, $"{packageId} has no '{path}' in its native payload.");

        using var source = entry!.Open();
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        return buffer.ToArray();
    }
}

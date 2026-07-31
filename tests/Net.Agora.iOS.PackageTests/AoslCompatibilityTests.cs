using System.Diagnostics;
using System.IO.Compression;

namespace Net.Agora.iOS.PackageTests;

/// <summary>
/// Four of the packages here ship an <c>aosl.xcframework</c> — Agora's own infrastructure library,
/// which AgoraRtcKit, AgoraRtmKit and AgoraChat all link as an <c>@rpath</c> dependency. In an app
/// that references more than one of those packages they do not each get their own: every copy
/// unpacks to <c>Frameworks/aosl.framework</c>, the bundle carries exactly one, and which one is
/// whichever the build happened to keep. So every product in that app runs against a library it
/// may not have been built against.
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
/// wins, all of them still load</em>: every aosl symbol any SDK framework here imports must be
/// exported by every aosl any package here ships.
/// </para>
/// <para>
/// This reads real Mach-O symbol tables with <c>nm</c> rather than comparing version strings,
/// because a version string can agree while the exports do not — 1.3.9 both adds three symbols
/// and drops two relative to 1.3.5. The tests build iOS packages, so they only ever run where the
/// Apple toolchain is installed.
/// </para>
/// </remarks>
public class AoslCompatibilityTests
{
    private const string PayloadTargetFramework = "net8.0-ios18.0";

    /// <summary>The device slice: one architecture, so <c>nm</c> needs no <c>-arch</c>.</summary>
    private const string DeviceSlice = "ios-arm64";

    /// <summary>Every package that ships an aosl, and the SDK framework in it that links one.</summary>
    private static readonly (string PackageId, string Framework)[] Consumers =
    [
        (Packages.Video, "AgoraRtcKit"),
        (Packages.Voice, "AgoraRtcKit"),
        (Packages.Signaling, "AgoraRtmKit"),
        (Packages.Chat, "AgoraChat"),
    ];

    [Fact]
    public void Every_shipped_aosl_satisfies_every_sdk_framework_that_links_one()
    {
        using var workspace = new TempDirectory();

        // What the SDKs ask aosl for, pooled: any one of them may be the product left holding
        // another package's copy, so each copy has to answer for all of them at once.
        var required = new SortedSet<string>(StringComparer.Ordinal);
        var exportsByPackage = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var (packageId, framework) in Consumers)
        {
            var exports = Exported(Extract(workspace, packageId, "aosl", packageId));
            exportsByPackage[packageId] = exports;

            // Intersected with this package's own aosl rather than taken whole: a framework's
            // undefined symbols also cover libSystem, libc++ and the Objective-C runtime, and
            // only the ones aosl actually provides are aosl's problem.
            required.UnionWith(
                Undefined(Extract(workspace, packageId, framework, $"{packageId}.{framework}"))
                    .Where(exports.Contains));
        }

        Assert.True(required.Count > 0, "No aosl symbols were resolved at all — the payload layout must have changed.");

        var failures = exportsByPackage
            .Select(entry => (entry.Key, Missing: required.Except(entry.Value).ToList()))
            .Where(result => result.Missing.Count > 0)
            .Select(result =>
                $"{result.Key} ships an aosl missing {result.Missing.Count} symbol(s) another SDK " +
                $"here imports: {string.Join(", ", result.Missing.Take(5))}")
            .ToList();

        Assert.True(
            failures.Count == 0,
            "An app referencing more than one of these packages keeps a single aosl.framework, so " +
            "every shipped copy has to satisfy every SDK. See build/fetch-signaling.sh.\n" +
            string.Join("\n", failures));
    }

    /// <summary>
    /// Writes one framework's device-slice Mach-O out of a package's native payload to disk, since
    /// <c>nm</c> reads files rather than streams.
    /// </summary>
    private static string Extract(TempDirectory workspace, string packageId, string framework, string name)
    {
        using var package = Packages.OpenPackage(packageId);
        using var payload = Packages.OpenNativePayload(package, packageId, PayloadTargetFramework);

        var path = $"{framework}.xcframework/{DeviceSlice}/{framework}.framework/{framework}";
        var entry = payload.GetEntry(path);
        Assert.True(entry is not null, $"{packageId} has no '{path}' in its native payload.");

        var destination = Path.Combine(workspace.Path, name);
        using (var source = entry!.Open())
        using (var file = File.Create(destination))
        {
            source.CopyTo(file);
        }

        return destination;
    }

    private static SortedSet<string> Exported(string binary) =>
        // -gU: external, defined. The undefined ones are what the consumer side wants.
        Symbols(binary, "-gU", line => line.Length > 2 && line[1] != "U");

    private static SortedSet<string> Undefined(string binary) =>
        Symbols(binary, "-u", line => line.Length == 1);

    private static SortedSet<string> Symbols(string binary, string flag, Func<string[], bool> keep)
    {
        var nm = Process.Start(new ProcessStartInfo("nm", $"{flag} \"{binary}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        Assert.True(nm is not null, "could not start nm");

        var output = nm!.StandardOutput.ReadToEnd();
        nm.StandardError.ReadToEnd();
        nm.WaitForExit();

        var symbols = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var raw in output.Split('\n'))
        {
            var fields = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length > 0 && keep(fields))
            {
                symbols.Add(fields[^1]);
            }
        }

        Assert.True(symbols.Count > 0, $"nm {flag} returned no symbols for '{binary}'.");
        return symbols;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() => Directory.CreateDirectory(Path);

        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"agora-aosl-{Guid.NewGuid():N}");

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

using System.IO.Compression;

namespace Net.Agora.iOS.PackageTests;

/// <summary>
/// Asserts the shape of the produced NuGet packages — target frameworks present, native payload
/// carried alongside the binding assembly, iOS-only slices inside it. Runs against the packed
/// .nupkg, not the build output, so it catches packaging regressions the compiler cannot see.
/// </summary>
public class PackageLayoutTests
{
    /// <summary>
    /// The heavyweight payload checks decompress tens of megabytes, and the payload is identical
    /// across target frameworks, so they run against one rather than all three. That the others
    /// carry the same content is asserted separately and cheaply by
    /// <see cref="Native_payload_is_the_same_across_target_frameworks"/>.
    /// </summary>
    private const string PayloadTargetFramework = "net8.0-ios18.0";

    [Theory]
    [MemberData(nameof(Packages.PackageFrameworks), MemberType = typeof(Packages))]
    public void Package_carries_a_binding_assembly_for_every_target_framework(string packageId, string tfm)
    {
        using var package = Packages.OpenPackage(packageId);

        var expected = $"lib/{tfm}/{packageId}.dll";
        Assert.True(package.GetEntry(expected) is not null, $"{packageId} is missing '{expected}'.");
    }

    [Theory]
    [MemberData(nameof(Packages.PackageFrameworks), MemberType = typeof(Packages))]
    public void Package_carries_the_native_payload_for_every_target_framework(string packageId, string tfm)
    {
        using var package = Packages.OpenPackage(packageId);

        var entry = package.GetEntry(Packages.ResourcesEntry(packageId, tfm));

        // The payload must be a single .resources.zip rather than a .resources directory. The
        // iOS SDK emits the directory form unless CompressBindingResourcePackage is set, which
        // puts every framework file at paths long enough to break restore on Windows — see the
        // comment in src/Agora.Binding.props.
        Assert.True(
            entry is not null,
            $"{packageId} is missing '{Packages.ResourcesEntry(packageId, tfm)}'. " +
            "Has CompressBindingResourcePackage been unset?");

        // The native payload is megabytes even compressed, with a floor per package (see
        // Packages.All). Anything near it means an empty placeholder was packed.
        Assert.True(
            entry!.Length > Packages.MinPayloadBytesOf(packageId),
            $"'{entry.FullName}' is only {entry.Length} bytes.");
    }

    [Theory]
    [MemberData(nameof(Packages.PackageFrameworks), MemberType = typeof(Packages))]
    public void Package_ships_every_xcframework_beside_the_binding_assembly(string packageId, string tfm)
    {
        // All of these are dynamic frameworks, so none can be linked into the assembly itself —
        // see the NativeReference comments in each binding's csproj. If one goes missing, every
        // consumer breaks at the app's link step — or, for Video's video_dec, at launch, since
        // nothing links it but AgoraRtcKit's own binary loads it — which no other test here would
        // catch. The expected list is per package: the voice build of AgoraRtcKit deliberately
        // carries no video codec.
        var names = Packages.NativePayload(packageId, tfm);

        Assert.True(names.Count > 0, $"{packageId} carries no native payload for {tfm}.");

        foreach (var framework in Packages.XcFrameworksOf(packageId))
        {
            Assert.Contains(names, n => n.Contains($"{framework}.xcframework/", StringComparison.Ordinal));
        }
    }

    [Theory]
    [MemberData(nameof(Packages.PackageIds), MemberType = typeof(Packages))]
    public void Native_payload_is_the_same_across_target_frameworks(string packageId)
    {
        using var package = Packages.OpenPackage(packageId);

        // net8/net9 are packed by the .NET 9 SDK and net10 by the .NET 10 one, and
        // merge-packages.py then grafts the net10 lib/ tree into the other package. Nothing in
        // that flow guarantees the two passes zipped the same frameworks, so this is where a
        // mismatched graft would be caught.
        //
        // Compared by logical content, not bytes: each pass re-zips the payload and the archives
        // embed their own timestamps, so the same frameworks legitimately produce different CRCs.
        var manifests = new List<(string Tfm, List<(string Name, long Length)> Entries)>();

        foreach (var tfm in Packages.TargetFrameworks)
        {
            // Opened and released one at a time — three payloads at once would be hundreds of MB.
            using var payload = Packages.OpenNativePayload(package, packageId, tfm);
            manifests.Add((tfm, payload.Entries
                .Select(e => (e.FullName, e.Length))
                .OrderBy(e => e.FullName, StringComparer.Ordinal)
                .ToList()));
        }

        var reference = manifests[0];
        foreach (var (tfm, entries) in manifests.Skip(1))
        {
            Assert.True(
                reference.Entries.SequenceEqual(entries),
                $"The native payload for {tfm} differs from {reference.Tfm} in {packageId}.");
        }
    }

    [Theory]
    [MemberData(nameof(Packages.PackageIds), MemberType = typeof(Packages))]
    public void Native_payload_carries_ios_slices_only(string packageId)
    {
        using var package = Packages.OpenPackage(packageId);
        using var payload = Packages.OpenNativePayload(package, packageId, PayloadTargetFramework);

        foreach (var framework in Packages.XcFrameworksOf(packageId))
        {
            var slices = SlicesOf(payload, framework);

            // Device and simulator, and nothing else. Agora ships no macOS or Mac Catalyst slice
            // for these SDKs (see the fetch scripts), so one appearing here would mean the fetch
            // staged something new and the package silently grew. Checked by shape rather than by
            // name so an upstream slice rename does not break the suite.
            Assert.All(slices, slice => Assert.True(
                Packages.IsIosSlice(slice),
                $"{framework}.xcframework carries a non-iOS slice '{slice}'."));

            Assert.Single(slices, Packages.IsSimulatorSlice);
            Assert.Single(slices, slice => !Packages.IsSimulatorSlice(slice));
        }
    }

    [Theory]
    [MemberData(nameof(Packages.PackageIds), MemberType = typeof(Packages))]
    public void Xcframework_manifests_match_the_slices_actually_shipped(string packageId)
    {
        using var package = Packages.OpenPackage(packageId);
        using var payload = Packages.OpenNativePayload(package, packageId, PayloadTargetFramework);

        foreach (var framework in Packages.XcFrameworksOf(packageId))
        {
            var manifest = payload.GetEntry($"{framework}.xcframework/Info.plist");
            Assert.True(manifest is not null, $"{framework}.xcframework has no Info.plist.");

            using var reader = new StreamReader(manifest!.Open());
            var text = reader.ReadToEnd();

            // An AvailableLibraries entry whose directory does not exist makes the iOS SDK reject
            // the whole xcframework — a failure that would only surface in a consuming app's
            // build. The reverse (a directory the manifest does not advertise) is invisible dead
            // weight. Both directions are covered: no macOS advertised, and every slice on disk
            // advertised.
            Assert.DoesNotContain("macos", text, StringComparison.OrdinalIgnoreCase);

            foreach (var slice in SlicesOf(payload, framework))
            {
                Assert.Contains(slice, text, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>The slice directory names an xcframework actually carries inside the payload.</summary>
    private static List<string> SlicesOf(ZipArchive payload, string framework) =>
        payload.Entries
            .Where(e => e.FullName.StartsWith($"{framework}.xcframework/", StringComparison.Ordinal))
            .Select(e => e.FullName.Split('/'))
            .Where(parts => parts.Length > 2)
            .Select(parts => parts[1])
            .Where(slice => !slice.EndsWith(".plist", StringComparison.Ordinal))
            .Distinct()
            .OrderBy(slice => slice, StringComparer.Ordinal)
            .ToList();
}

using System.Xml.Linq;

namespace Net.Agora.iOS.PackageTests;

/// <summary>
/// Asserts the shape of the optional RTC feature extension packages. They are not bindings — see
/// src/Agora.Extension.md — so what is worth checking about them is almost the inverse of
/// <see cref="PackageLayoutTests"/>: the framework has to be there, under the name Agora actually
/// gives it, for every target framework; the managed assembly is expected to be an empty stub; and
/// the package must pull in neither RTC binding.
/// </summary>
public class ExtensionPackageTests
{
    [Theory]
    [MemberData(nameof(Packages.ExtensionFrameworks), MemberType = typeof(Packages))]
    public void Extension_carries_its_xcframeworks_for_every_target_framework(string packageId, string tfm)
    {
        var payload = Packages.NativePayload(packageId, tfm);

        Assert.True(payload.Count > 0, $"{packageId} has no native payload for {tfm}.");

        // The names matter more here than anywhere else in this suite: Agora's marketing name for
        // an extension and its framework name do not match (VirtualBackground ships
        // AgoraVideoSegmentationExtension), so the mapping lives in build/fetch-extensions.sh and
        // this is what holds the packed result to it.
        foreach (var framework in Packages.ExtensionXcFrameworksOf(packageId))
        {
            Assert.True(
                payload.Any(e => e.StartsWith($"{framework}.xcframework/", StringComparison.Ordinal)),
                $"{packageId} ({tfm}) is missing {framework}.xcframework. " +
                $"Payload: {string.Join(", ", payload.Select(e => e.Split('/')[0]).Distinct())}");
        }
    }

    [Theory]
    [MemberData(nameof(Packages.ExtensionFrameworks), MemberType = typeof(Packages))]
    public void Extension_ships_both_the_device_and_the_simulator_slice(string packageId, string tfm)
    {
        var payload = Packages.NativePayload(packageId, tfm);

        var slices = payload
            .Select(e => e.Split('/'))
            .Where(parts => parts.Length > 1)
            .Select(parts => parts[1])
            .Where(Packages.IsIosSlice)
            .Distinct()
            .ToList();

        // A device-only payload builds and links, then fails at simulator launch — the failure
        // mode worth a test, since nothing before runtime notices.
        Assert.Contains(slices, s => !Packages.IsSimulatorSlice(s));
        Assert.Contains(slices, Packages.IsSimulatorSlice);
    }

    [Theory]
    [MemberData(nameof(Packages.ExtensionIds), MemberType = typeof(Packages))]
    public void Extension_depends_on_neither_RTC_binding(string packageId)
    {
        using var package = Packages.OpenPackage(packageId);

        var nuspec = package.Entries.Single(e => e.Name.EndsWith(".nuspec", StringComparison.Ordinal));
        using var stream = nuspec.Open();
        var document = XDocument.Load(stream);

        // The <dependency> elements specifically, not the nuspec text: each package's description
        // names both RTC packages on purpose, to tell the reader what to add this alongside.
        var dependencies = document.Descendants()
            .Where(e => e.Name.LocalName == "dependency")
            .Select(e => (string?)e.Attribute("id") ?? "")
            .ToList();

        // The Video and Voice bindings are mutually exclusive in one app, so a dependency on
        // either would force a flavour on the consumer — and the audio extensions work with both.
        Assert.DoesNotContain("Net.Agora.Video.iOS", dependencies);
        Assert.DoesNotContain("Net.Agora.Voice.iOS", dependencies);
    }

    [Theory]
    [MemberData(nameof(Packages.ExtensionIds), MemberType = typeof(Packages))]
    public void Extension_ships_a_stub_assembly_rather_than_a_binding(string packageId)
    {
        using var package = Packages.OpenPackage(packageId);

        foreach (var tfm in Packages.TargetFrameworks)
        {
            var entry = package.GetEntry($"lib/{tfm}/{packageId}.dll");
            Assert.True(entry is not null, $"{packageId} is missing the assembly for {tfm}.");

            // Documenting the intent rather than guarding a regression: if one of these frameworks
            // ever grows a public Objective-C surface worth binding, this is where it will show up.
            Assert.True(
                entry!.Length < 100_000,
                $"{packageId}'s assembly for {tfm} is {entry.Length} bytes — that is a real " +
                "binding, not the expected stub. Did ApiDefinition.cs grow types?");
        }
    }
}

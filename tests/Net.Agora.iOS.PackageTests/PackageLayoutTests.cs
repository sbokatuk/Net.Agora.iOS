namespace Net.Agora.iOS.PackageTests;

/// <summary>
/// Asserts the shape of the produced NuGet packages — target frameworks present, native payload
/// carried alongside the binding assembly. Runs against the packed .nupkg, not the build output.
/// </summary>
public class PackageLayoutTests
{
    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void Video_carries_a_binding_assembly_for_every_target_framework(string tfm)
    {
        using var package = Packages.OpenPackage(Packages.Video);

        var expected = $"lib/{tfm}/{Packages.Video}.dll";
        Assert.True(package.GetEntry(expected) is not null, $"{Packages.Video} is missing '{expected}'.");
    }

    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void Video_ships_every_xcframework_beside_the_binding_assembly(string tfm)
    {
        // All five are dynamic frameworks, so none can be linked into the assembly itself — see
        // the NativeReference comment in Net.Agora.Video.iOS.csproj. If one goes missing, every
        // consumer breaks at link time, which no other test here would catch.
        var names = Packages.NativePayload(Packages.Video, tfm);

        Assert.True(names.Count > 0, $"{Packages.Video} carries no native payload for {tfm}.");

        foreach (var framework in new[] { "AgoraRtcKit", "aosl", "Agorafdkaac", "Agoraffmpeg", "AgoraSoundTouch" })
        {
            Assert.Contains(names, n => n.Contains($"{framework}.xcframework/", StringComparison.Ordinal));
        }
    }
}

namespace Net.Agora.iOS.PackageTests;

/// <summary>
/// Asserts the shape of the Fastboard package. Like Whiteboard it is built from source into a
/// static library, and unlike anything else here what it binds is this repository's own
/// Objective-C shim rather than the SDK — so the header that shim generates has to be in the
/// package, and both resource bundles have to reach the app.
/// </summary>
public class FastboardPackageTests
{
    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void Fastboard_carries_its_static_library_for_every_target_framework(string tfm)
    {
        using var package = Packages.OpenPackage(Packages.Fastboard);

        var entry = package.GetEntry(Packages.ResourcesEntry(Packages.Fastboard, tfm));
        Assert.True(entry is not null, $"{Packages.Fastboard} has no native payload for {tfm}.");
        Assert.True(
            entry!.Length > Packages.FastboardMinPayloadBytes,
            $"{Packages.Fastboard}'s payload for {tfm} is only {entry.Length} bytes; looks empty.");

        var payload = Packages.NativePayload(Packages.Fastboard, tfm);
        Assert.Contains(payload, e => e.StartsWith("AgoraFastboardBridge.xcframework/", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void Fastboard_ships_both_the_device_and_the_simulator_slice(string tfm)
    {
        var slices = Packages.NativePayload(Packages.Fastboard, tfm)
            .Select(e => e.Split('/'))
            .Where(parts => parts.Length > 1)
            .Select(parts => parts[1])
            .Where(Packages.IsIosSlice)
            .Distinct()
            .ToList();

        Assert.Contains(slices, s => !Packages.IsSimulatorSlice(s));
        Assert.Contains(slices, Packages.IsSimulatorSlice);
    }

    [Fact]
    public void Fastboard_ships_both_resource_bundles_and_the_targets_that_install_them()
    {
        using var package = Packages.OpenPackage(Packages.Fastboard);
        var names = package.Entries.Select(e => e.FullName).ToList();

        // Fastboard's own assets are the toolbar; the whiteboard SDK's are the board page. Missing
        // the first gives blank buttons, missing the second a blank board, and neither reports an
        // error — which is the whole reason to check.
        Assert.Contains(names, n => n.StartsWith("fastboard/Fastboard_Fastboard.bundle/", StringComparison.Ordinal));
        Assert.Contains(names, n => n == "fastboard/Whiteboard_Whiteboard.bundle/index.html");
        Assert.Contains(names, n => n == $"buildTransitive/{Packages.Fastboard}.targets");
    }

    [Fact]
    public void Fastboard_ships_the_bundle_its_error_messages_are_looked_up_in()
    {
        using var package = Packages.OpenPackage(Packages.Fastboard);
        var names = package.Entries.Select(e => e.FullName).ToList();

        // SwiftPM doesn't produce this one, so build-fastboard.sh does. Without it a consumer gets
        // the bare key "joinRoomError" instead of a message.
        const string Bundle = "fastboard/Fastboard_Fastboard.bundle/LocalizedStrings.bundle";

        Assert.Contains(names, n => n == $"{Bundle}/Info.plist");
        Assert.Contains(names, n => n == $"{Bundle}/en.lproj/Localizable.strings");
        Assert.Contains(names, n => n == $"{Bundle}/zh-Hans.lproj/Localizable.strings");
    }
}

namespace Net.Agora.iOS.PackageTests;

/// <summary>
/// Asserts the shape of the Whiteboard package, which differs from every other one here: it is
/// built from netless's source into a static library, so the native payload is embedded in the
/// assembly rather than carried beside it, and what travels in the resource zip is the board's
/// JavaScript.
/// </summary>
public class WhiteboardPackageTests
{
    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void Whiteboard_carries_its_static_library_for_every_target_framework(string tfm)
    {
        using var package = Packages.OpenPackage(Packages.Whiteboard);

        var entry = package.GetEntry(Packages.ResourcesEntry(Packages.Whiteboard, tfm));
        Assert.True(entry is not null, $"{Packages.Whiteboard} has no native payload for {tfm}.");
        Assert.True(
            entry!.Length > Packages.WhiteboardMinPayloadBytes,
            $"{Packages.Whiteboard}'s payload for {tfm} is only {entry.Length} bytes; looks empty.");

        var payload = Packages.NativePayload(Packages.Whiteboard, tfm);
        Assert.Contains(payload, e => e.StartsWith("Whiteboard.xcframework/", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void Whiteboard_ships_both_the_device_and_the_simulator_slice(string tfm)
    {
        var slices = Packages.NativePayload(Packages.Whiteboard, tfm)
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
    public void Whiteboard_ships_the_board_javascript_and_the_targets_that_install_it()
    {
        using var package = Packages.OpenPackage(Packages.Whiteboard);
        var names = package.Entries.Select(e => e.FullName).ToList();

        // The board is a web view. Ship it without its page and the view comes up blank with no
        // error anywhere — the failure this test exists for. The bundle's directory name is
        // SwiftPM's own, and the library looks it up by that exact string.
        Assert.Contains(names, n => n == "whiteboard/Whiteboard_Whiteboard.bundle/index.html");

        // And the half that puts it in the app: a binding package cannot ship a BundleResource of
        // its own, so the bundle is plain content plus a .targets that re-declares it.
        Assert.Contains(names, n => n == $"buildTransitive/{Packages.Whiteboard}.targets");
    }
}

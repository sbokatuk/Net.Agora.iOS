using System.IO.Compression;

namespace Net.Agora.iOS.PackageTests;

/// <summary>Locates the packed .nupkg files and describes what each is expected to contain.</summary>
public static class Packages
{
    public const string Video = "Net.Agora.Video.iOS";
    public const string Voice = "Net.Agora.Voice.iOS";
    public const string Signaling = "Net.Agora.Signaling.iOS";
    public const string Chat = "Net.Agora.Chat.iOS";
    public const string Whiteboard = "Net.Agora.Whiteboard.iOS";
    public const string Fastboard = "Net.Agora.Fastboard.iOS";

    /// <summary>
    /// Every package build/packages.tsv lists, with the xcframeworks it ships: the API surface
    /// (AgoraRtcKit) plus the codec and infrastructure frameworks its binary links against.
    /// Mirrors each project's NativeReference list and its fetch script's staging loop — a name
    /// missing from either shows up here. Pinned rather than parsed from the .tsv: a package
    /// silently dropped from the .tsv (and so from the pack) is a regression these tests should
    /// catch, not adapt to.
    /// </summary>
    /// <remarks>
    /// video_dec matters most: the *video* build of AgoraRtcKit links it directly, so a Video
    /// payload without it kills every consuming app at launch — a dyld failure the compiler and
    /// linker never see. That is how it was found in the first place; see the csproj. The voice
    /// build of AgoraRtcKit links no video codec at all, which is exactly what Voice pays for:
    /// the list difference below is the size difference consumers pick the package for.
    /// </remarks>
    public static readonly (string Id, string[] XcFrameworks, long MinPayloadBytes)[] All =
    [
        (Video, ["AgoraRtcKit", "aosl", "Agorafdkaac", "Agoraffmpeg", "AgoraSoundTouch", "video_dec"], 10_000_000),
        (Voice, ["AgoraRtcKit", "aosl", "Agorafdkaac", "Agoraffmpeg", "AgoraSoundTouch"], 10_000_000),
        // Signaling is a smaller product: two frameworks, and the fetch script strips the xros
        // slices Agora ships only for this SDK.
        (Signaling, ["AgoraRtmKit", "aosl"], 4_000_000),
        // Chat is smaller still, and the only product whose aosl comes from a second archive
        // (AgoraInfra_iOS) rather than from the SDK zip itself — see build/fetch-chat.sh.
        (Chat, ["AgoraChat", "aosl"], 4_000_000),
        // Whiteboard is the odd one out: a static library this repository builds from netless's
        // source rather than a dynamic framework Agora publishes, so it is embedded in the
        // assembly rather than carried beside it — which is why ExtensionPackageTests' payload
        // helpers do not apply to it and WhiteboardPackageTests checks it instead.
    ];

    /// <summary>
    /// Whiteboard's payload floor. Lower than the others' because what it carries is a static
    /// library built from netless's source rather than a dynamic framework with both slices of a
    /// full media SDK in it.
    /// </summary>
    public const long WhiteboardMinPayloadBytes = 500_000;

    /// <summary>
    /// Fastboard's payload floor. Higher than Whiteboard's because its static library contains the
    /// whole whiteboard SDK as well as Fastboard and this repository's shim.
    /// </summary>
    public const long FastboardMinPayloadBytes = 1_000_000;

    public static string[] XcFrameworksOf(string packageId) =>
        All.Single(p => p.Id == packageId).XcFrameworks;

    public static long MinPayloadBytesOf(string packageId) =>
        All.Single(p => p.Id == packageId).MinPayloadBytes;

    /// <summary>
    /// The optional RTC feature extensions, with the xcframework(s) each must carry. Deliberately
    /// a separate table from <see cref="All"/>: these packages bind nothing (their ApiDefinition.cs
    /// files are empty on purpose — see src/Agora.Extension.md), so the payload-size floors that
    /// exist to catch a truncated SDK download would be beside the point. What matters is that the
    /// framework named here is the one that actually shipped, since the mapping from a package's
    /// name to Agora's framework name is not derivable and lives in build/fetch-extensions.sh.
    /// </summary>
    public static readonly (string Id, string[] XcFrameworks)[] Extensions =
    [
        ("Net.Agora.Extensions.Ains.iOS", ["AgoraAiNoiseSuppressionExtension"]),
        ("Net.Agora.Extensions.Aiaec.iOS", ["AgoraAiEchoCancellationExtension"]),
        ("Net.Agora.Extensions.AudioBeauty.iOS", ["AgoraAudioBeautyExtension"]),
        ("Net.Agora.Extensions.SpatialAudio.iOS", ["AgoraSpatialAudioExtension"]),
        ("Net.Agora.Extensions.VirtualBackground.iOS", ["AgoraVideoSegmentationExtension"]),
        ("Net.Agora.Extensions.ContentInspect.iOS", ["AgoraContentInspectExtension"]),
        ("Net.Agora.Extensions.ClearVision.iOS", ["AgoraClearVisionExtension"]),
        ("Net.Agora.Extensions.FaceCapture.iOS", ["AgoraFaceCaptureExtension"]),
        ("Net.Agora.Extensions.FaceDetection.iOS", ["AgoraFaceDetectionExtension"]),
        ("Net.Agora.Extensions.VideoQualityAnalyzer.iOS", ["AgoraVideoQualityAnalyzerExtension"]),
        // The only two-framework extension: the encoder plugin and the codec it wraps.
        ("Net.Agora.Extensions.VideoEncoder.iOS", ["AgoraVideoEncoderExtension", "video_enc"]),
        ("Net.Agora.Extensions.Av1Encoder.iOS", ["AgoraVideoAv1EncoderExtension"]),
    ];

    /// <summary>Every (extension package, target framework) pair.</summary>
    public static IEnumerable<object[]> ExtensionFrameworks =>
        Extensions.SelectMany(e => TargetFrameworks.Select(tfm => new object[] { e.Id, tfm }));

    /// <summary>Every extension package id.</summary>
    public static IEnumerable<object[]> ExtensionIds =>
        Extensions.Select(e => new object[] { e.Id });

    public static string[] ExtensionXcFrameworksOf(string packageId) =>
        Extensions.Single(e => e.Id == packageId).XcFrameworks;

    /// <summary>
    /// Target frameworks every package here must carry, one per SDK band pass. Pinned rather than
    /// discovered: a package that silently lost a target framework because a pack pass failed is
    /// exactly the regression these tests exist to catch.
    /// </summary>
    public static readonly string[] TargetFrameworks =
    [
        "net8.0-ios18.0", "net9.0-ios18.0", "net10.0-ios26.0",
    ];

    public static IEnumerable<object[]> Frameworks =>
        TargetFrameworks.Select(tfm => new object[] { tfm });

    /// <summary>Every package id — the axis of the per-package facts.</summary>
    public static IEnumerable<object[]> PackageIds =>
        All.Select(p => new object[] { p.Id });

    /// <summary>Every (package, target framework) pair — the axis most tests run over.</summary>
    public static IEnumerable<object[]> PackageFrameworks =>
        All.SelectMany(p => TargetFrameworks.Select(tfm => new object[] { p.Id, tfm }));

    /// <summary>
    /// Identifies the simulator slice of an xcframework from its directory name. Slice names are
    /// checked by shape rather than pinned literally (Agora ships ios-arm64 and
    /// ios-arm64_x86_64-simulator today, but the exact strings are upstream's to change — the
    /// FFmpegKit repository has already watched a device slice get renamed under it).
    /// </summary>
    public static bool IsSimulatorSlice(string slice) =>
        slice.Contains("simulator", StringComparison.Ordinal);

    /// <summary>Whether a slice directory name denotes an iOS slice at all.</summary>
    public static bool IsIosSlice(string slice) =>
        slice.StartsWith("ios-", StringComparison.Ordinal);

    /// <summary>
    /// The binding's native payload entry for a target framework: the xcframeworks, zipped by the
    /// iOS SDK into a single &lt;assembly&gt;.resources.zip beside the binding assembly.
    /// CompressBindingResourcePackage=true in Agora.Binding.props means it is always the zip,
    /// never a loose .resources directory — see the comment there for why.
    /// </summary>
    public static string ResourcesEntry(string packageId, string tfm) =>
        $"lib/{tfm}/{packageId}.resources.zip";

    /// <summary>The names of everything in a binding package's native payload for one target framework.</summary>
    public static IReadOnlyList<string> NativePayload(string packageId, string tfm)
    {
        using var package = OpenPackage(packageId);

        var zipped = package.GetEntry(ResourcesEntry(packageId, tfm));
        if (zipped is null)
        {
            return [];
        }

        using var archive = new ZipArchive(zipped.Open());
        return archive.Entries.Select(e => e.FullName).ToList();
    }

    /// <summary>Reads a package entry fully into memory so it can be seeked.</summary>
    public static MemoryStream ReadEntry(ZipArchive package, string entryName)
    {
        var entry = package.GetEntry(entryName);
        Assert.True(entry is not null, $"Package has no entry '{entryName}'.");

        var buffer = new MemoryStream();
        using (var stream = entry!.Open())
        {
            stream.CopyTo(buffer);
        }

        buffer.Position = 0;
        return buffer;
    }

    /// <summary>
    /// Opens the binding resource package for a target framework. The native payload is a zip
    /// nested inside the .nupkg, so its contents are only reachable through a second archive.
    /// </summary>
    public static ZipArchive OpenNativePayload(ZipArchive package, string packageId, string tfm) =>
        new(ReadEntry(package, ResourcesEntry(packageId, tfm)));

    public static string ArtifactsDirectory { get; } = ResolveArtifactsDirectory();

    public static string FindPackage(string packageId, string extension = ".nupkg")
    {
        var matches = Directory.Exists(ArtifactsDirectory)
            ? Directory.GetFiles(ArtifactsDirectory, $"{packageId}.*{extension}")
                .Where(f => IsVersionOf(packageId, Path.GetFileName(f), extension))
                .ToArray()
            : [];

        Assert.True(
            matches.Length > 0,
            $"No {packageId}.<version>{extension} found in '{ArtifactsDirectory}'. " +
            "Run build/BuildNugets.sh first.");

        // A rebuilt working copy can leave several versions behind; test the newest.
        return matches.OrderByDescending(File.GetLastWriteTimeUtc).First();
    }

    private static bool IsVersionOf(string packageId, string fileName, string extension)
    {
        var remainder = fileName[(packageId.Length + 1)..^extension.Length];
        return remainder.Length > 0 && char.IsDigit(remainder[0]);
    }

    public static ZipArchive OpenPackage(string packageId, string extension = ".nupkg") =>
        ZipFile.OpenRead(FindPackage(packageId, extension));

    private static string ResolveArtifactsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? AppContext.BaseDirectory;

        return Environment.GetEnvironmentVariable("AGORA_ARTIFACTS") is { Length: > 0 } configured
            ? Path.GetFullPath(configured, root)
            : Path.Combine(root, "artifacts");
    }
}

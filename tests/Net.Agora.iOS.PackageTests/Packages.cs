using System.IO.Compression;

namespace Net.Agora.iOS.PackageTests;

/// <summary>Locates the packed .nupkg files and describes what each is expected to contain.</summary>
public static class Packages
{
    public const string Video = "Net.Agora.Video.iOS";

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

    /// <summary>
    /// The names of everything in a binding package's native payload for one target framework.
    /// CompressBindingResourcePackage=true means this is always a &lt;assembly&gt;.resources.zip,
    /// never a loose directory.
    /// </summary>
    public static IReadOnlyList<string> NativePayload(string packageId, string tfm)
    {
        using var package = OpenPackage(packageId);

        var zipped = package.GetEntry($"lib/{tfm}/{packageId}.resources.zip");
        if (zipped is null)
        {
            return [];
        }

        using var archive = new ZipArchive(zipped.Open());
        return archive.Entries.Select(e => e.FullName).ToList();
    }

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

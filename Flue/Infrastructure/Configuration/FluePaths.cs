namespace Flue.Infrastructure.Configuration;

public sealed record FluePaths (string ProjectRoot)
{
    public string SourceRoot { get; } = Path.Combine(ProjectRoot, "src");

    public string FlutterBridgeRoot { get; } = Path.Combine(ProjectRoot, "flutter_bridge");

    public string DartLibRoot { get; } = Path.Combine(ProjectRoot, "flutter_bridge", "lib");

    public string SourceRouterFile { get; } = Path.Combine(ProjectRoot, "src", "router.ts");

    public string RootRouterFile { get; } = Path.Combine(ProjectRoot, "router.ts");

    public ImmutableArray<string> RouterCandidates { get; } =
    [
        Path.Combine(ProjectRoot, "src", "router.ts"),
        Path.Combine(ProjectRoot, "router.ts")
    ];

    public void EnsureBaseDirectories ()
    {
        Directory.CreateDirectory(SourceRoot);
        Directory.CreateDirectory(DartLibRoot);
    }

    public string ToRelativeSourcePath (string sourcePath)
    {
        return Path.GetRelativePath(SourceRoot, sourcePath).Replace('\\', '/');
    }

    public string ToDartFilePath (string sourceFilePath)
    {
        var relative = Path.GetRelativePath(SourceRoot, sourceFilePath);
        var dartRelative = Path.ChangeExtension(relative, ".dart");
        return Path.Combine(DartLibRoot, dartRelative);
    }

    public string ToDartDirectoryPath (string sourceDirectoryPath)
    {
        var relative = Path.GetRelativePath(SourceRoot, sourceDirectoryPath);
        return Path.Combine(DartLibRoot, relative);
    }

    public bool IsRouterFile (string path)
    {
        var fullPath = Path.GetFullPath(path);
        return RouterCandidates.Any(candidate =>
            string.Equals(fullPath, Path.GetFullPath(candidate), StringComparison.OrdinalIgnoreCase));
    }

    public string? ResolveRouterFile ()
    {
        foreach (var candidate in RouterCandidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}

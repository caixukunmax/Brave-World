namespace GameServer.Tables;

public static class RuntimeDataPathResolver
{
    public static string ResolveMapDataDir(
        string? configuredPath = null,
        string? baseDirectory = null,
        string? currentDirectory = null)
    {
        var logicalPath = string.IsNullOrWhiteSpace(configuredPath) ? "data" : configuredPath;
        var candidates = EnumerateCandidates(
                new[] { logicalPath }.Concat(GetRepoFallbacks(logicalPath)),
                baseDirectory,
                currentDirectory)
            .ToList();

        var resolved = candidates.FirstOrDefault(dir => File.Exists(Path.Combine(dir, "map_registry.json")));
        return resolved ?? candidates.First();
    }

    public static string? FindTablesDir(
        string? configuredDataDir = null,
        string? baseDirectory = null,
        string? currentDirectory = null)
    {
        var logicalDataDir = string.IsNullOrWhiteSpace(configuredDataDir) ? "data" : configuredDataDir;
        var resolvedDataDir = ResolveMapDataDir(logicalDataDir, baseDirectory, currentDirectory);
        var candidatePaths = new List<string>
        {
            Path.Combine(resolvedDataDir, "tables"),
            Path.Combine(logicalDataDir, "tables"),
        };

        candidatePaths.AddRange(GetRepoFallbacks(logicalDataDir).Select(path => Path.Combine(path, "tables")));

        return EnumerateCandidates(candidatePaths, baseDirectory, currentDirectory)
            .FirstOrDefault(dir => Directory.Exists(dir) && Directory.GetFiles(dir, "*.json").Length > 0);
    }

    private static IEnumerable<string> GetRepoFallbacks(string logicalPath)
    {
        if (Path.IsPathRooted(logicalPath))
        {
            yield break;
        }

        var normalized = logicalPath.Replace('\\', '/');
        if (!normalized.StartsWith("servercsharp/", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine("servercsharp", logicalPath);
        }
    }

    private static IEnumerable<string> EnumerateCandidates(
        IEnumerable<string> candidatePaths,
        string? baseDirectory,
        string? currentDirectory)
    {
        var effectiveBaseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppContext.BaseDirectory
            : baseDirectory;
        var effectiveCurrentDirectory = string.IsNullOrWhiteSpace(currentDirectory)
            ? Directory.GetCurrentDirectory()
            : currentDirectory;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidatePath in candidatePaths)
        {
            if (string.IsNullOrWhiteSpace(candidatePath))
            {
                continue;
            }

            foreach (var expanded in ExpandCandidate(candidatePath, effectiveBaseDirectory, effectiveCurrentDirectory))
            {
                if (seen.Add(expanded))
                {
                    yield return expanded;
                }
            }
        }
    }

    private static IEnumerable<string> ExpandCandidate(
        string candidatePath,
        string baseDirectory,
        string currentDirectory)
    {
        if (Path.IsPathRooted(candidatePath))
        {
            yield return Path.GetFullPath(candidatePath);
            yield break;
        }

        yield return Path.GetFullPath(Path.Combine(baseDirectory, candidatePath));
        yield return Path.GetFullPath(Path.Combine(currentDirectory, candidatePath));
        yield return Path.GetFullPath(candidatePath);
    }
}
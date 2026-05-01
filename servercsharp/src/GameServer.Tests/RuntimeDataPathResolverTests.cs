using GameServer.Tables;
using Xunit;

namespace GameServer.Tests;

public class RuntimeDataPathResolverTests
{
    [Fact]
    public void ResolveMapDataDir_PrefersCopiedOutputDataDirectory()
    {
        using var sandbox = new PathSandbox();
        var outputDir = sandbox.CreateDirectory("server-output");
        var currentDir = sandbox.CreateDirectory("repo-root");
        sandbox.WriteFile(Path.Combine(outputDir, "data", "map_registry.json"), "[]");

        var resolved = RuntimeDataPathResolver.ResolveMapDataDir(
            configuredPath: null,
            baseDirectory: outputDir,
            currentDirectory: currentDir);

        Assert.Equal(Path.Combine(outputDir, "data"), resolved);
    }

    [Fact]
    public void ResolveMapDataDir_FallsBackToRepoServerDataDirectory()
    {
        using var sandbox = new PathSandbox();
        var outputDir = sandbox.CreateDirectory("server-output");
        var repoRoot = sandbox.CreateDirectory("repo-root");
        sandbox.WriteFile(Path.Combine(repoRoot, "servercsharp", "data", "map_registry.json"), "[]");

        var resolved = RuntimeDataPathResolver.ResolveMapDataDir(
            configuredPath: null,
            baseDirectory: outputDir,
            currentDirectory: repoRoot);

        Assert.Equal(Path.Combine(repoRoot, "servercsharp", "data"), resolved);
    }

    [Fact]
    public void FindTablesDir_FindsTablesUnderResolvedDataDirectory()
    {
        using var sandbox = new PathSandbox();
        var outputDir = sandbox.CreateDirectory("server-output");
        var repoRoot = sandbox.CreateDirectory("repo-root");
        sandbox.WriteFile(Path.Combine(repoRoot, "servercsharp", "data", "map_registry.json"), "[]");
        sandbox.WriteFile(Path.Combine(repoRoot, "servercsharp", "data", "tables", "common_tbmonster.json"), "[]");

        var resolved = RuntimeDataPathResolver.FindTablesDir(
            configuredDataDir: null,
            baseDirectory: outputDir,
            currentDirectory: repoRoot);

        Assert.Equal(Path.Combine(repoRoot, "servercsharp", "data", "tables"), resolved);
    }

    private sealed class PathSandbox : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "tslua2-path-tests", Guid.NewGuid().ToString("N"));

        public string CreateDirectory(string relativePath)
        {
            var fullPath = Path.Combine(_root, relativePath);
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        public void WriteFile(string fullPath, string content)
        {
            var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("File path must have a directory");
            Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
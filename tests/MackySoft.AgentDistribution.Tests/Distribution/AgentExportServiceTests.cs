using System.IO.Compression;
using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentDistribution.Tests.Distribution;

public sealed class AgentExportServiceTests
{
    private static readonly DateTime ZipEntryTimestamp = new(1980, 1, 1, 0, 0, 0);

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_Directory_PublishesHostArtifactsAndResolvedSkillsInSeparateNamespaces ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-export", "directory");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var agent = AgentDistributionTestData.CreateAgent(skills, "planner", HostKind.Codex, "planner.toml");
        var catalog = AgentDistributionTestData.CreateCatalog(skills, [agent]);
        var outputPath = scope.GetPath("exported");
        var service = new AgentExportService(SkillTestData.CreateMaterializationService());

        var result = await service.ExportAsync(
            catalog,
            HostKind.Codex,
            AbsolutePath.Parse(outputPath),
            SkillExportFormat.Directory,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.True(result.Value!.IsSameAs(AbsolutePath.Parse(outputPath)));
        Assert.True(Directory.Exists(result.Value.Value));
        Assert.Equal("name = \"planner\"\n", await File.ReadAllTextAsync(Path.Combine(outputPath, "agents", "planner.toml")));
        Assert.False(Directory.Exists(Path.Combine(outputPath, "agents", "planner")));

        var materializationService = SkillTestData.CreateMaterializationService();
        foreach (var skill in skills)
        {
            var materialized = materializationService.Materialize(skill, HostKind.Codex);
            Assert.True(materialized.IsSuccess, materialized.Failure?.Message);
            foreach (var file in materialized.Value!.Files)
            {
                var path = Path.Combine(outputPath, "skills", skill.Manifest.SkillName.Value, file.RelativePath.Value.Replace('/', Path.DirectorySeparatorChar));
                Assert.Equal(file.Content, await File.ReadAllTextAsync(path));
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_Zip_WritesDeterministicSortedEntries ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-export", "zip");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var agent = AgentDistributionTestData.CreateAgent(skills, "planner", HostKind.Codex, "planner.toml");
        var catalog = AgentDistributionTestData.CreateCatalog(skills, [agent]);
        var service = new AgentExportService(SkillTestData.CreateMaterializationService());
        var firstPath = scope.GetPath("first.zip");
        var secondPath = scope.GetPath("second.zip");

        var firstResult = await service.ExportAsync(catalog, HostKind.Codex, AbsolutePath.Parse(firstPath), SkillExportFormat.Zip, CancellationToken.None);
        var secondResult = await service.ExportAsync(catalog, HostKind.Codex, AbsolutePath.Parse(secondPath), SkillExportFormat.Zip, CancellationToken.None);

        Assert.True(firstResult.IsSuccess, firstResult.Failure?.Message);
        Assert.True(secondResult.IsSuccess, secondResult.Failure?.Message);
        Assert.Equal(await File.ReadAllBytesAsync(firstPath), await File.ReadAllBytesAsync(secondPath));
        using var archive = ZipFile.OpenRead(firstPath);
        var entryNames = archive.Entries.Select(static entry => entry.FullName).ToArray();
        Assert.Equal(entryNames.Order(StringComparer.Ordinal), entryNames);
        Assert.Contains("agents/planner.toml", entryNames);
        Assert.All(archive.Entries, static entry => Assert.Equal(ZipEntryTimestamp, entry.LastWriteTime.DateTime));
        Assert.All(archive.Entries, static entry => Assert.DoesNotContain('\\', entry.FullName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_WhenAgentArtifactPathsCollide_FailsBeforeCreatingOutput ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-export", "artifact-collision");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var first = AgentDistributionTestData.CreateAgent(skills, "planner", HostKind.Codex, "shared.toml");
        var second = AgentDistributionTestData.CreateAgent(skills, "reviewer", HostKind.Codex, "shared.toml");
        var catalog = AgentDistributionTestData.CreateCatalog(skills, [second, first]);
        var outputPath = scope.GetPath("exported");
        var service = new AgentExportService(SkillTestData.CreateMaterializationService());

        var result = await service.ExportAsync(catalog, HostKind.Codex, AbsolutePath.Parse(outputPath), SkillExportFormat.Directory, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.False(Directory.Exists(outputPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_WhenExistingOutputIsSymlink_RejectsUnsafePath ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-export", "symlink-output");
        using var outsideScope = TestDirectories.CreateTempScope("agent-distribution-agent-export", "symlink-output-outside");
        var outputPath = scope.GetPath("exported");
        try
        {
            Directory.CreateSymbolicLink(outputPath, outsideScope.FullPath);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var agent = AgentDistributionTestData.CreateAgent(skills, "planner", HostKind.Codex, "planner.toml");
        var service = new AgentExportService(SkillTestData.CreateMaterializationService());

        var result = await service.ExportAsync(
            AgentDistributionTestData.CreateCatalog(skills, [agent]),
            HostKind.Codex,
            AbsolutePath.Parse(outputPath),
            SkillExportFormat.Directory,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(outsideScope.FullPath));
    }
}

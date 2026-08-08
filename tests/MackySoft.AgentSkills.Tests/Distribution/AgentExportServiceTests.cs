using System.IO.Compression;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Distribution;

public sealed class AgentExportServiceTests
{
    private static readonly DateTime ZipEntryTimestamp = new(1980, 1, 1, 0, 0, 0);

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_Directory_PublishesHostArtifactsAndResolvedSkillsInSeparateNamespaces ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-agent-export", "directory");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var agent = AgentDistributionTestData.CreateAgent(skills, "planning", "planner", AgentHostKind.OpenAi, "planner.toml");
        var catalog = AgentDistributionTestData.CreateCatalog(skills, [agent]);
        var outputPath = scope.GetPath("exported");
        var service = new AgentExportService(SkillTestData.CreateMaterializationService());

        var result = await service.ExportAsync(
            catalog,
            AgentHostKind.OpenAi,
            outputPath,
            SkillExportFormat.Directory,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(Path.GetFileName(outputPath), Path.GetFileName(result.Value));
        Assert.True(Directory.Exists(result.Value));
        Assert.Equal("name = \"planner\"\n", await File.ReadAllTextAsync(Path.Combine(outputPath, "agents", "planner.toml")));
        Assert.False(Directory.Exists(Path.Combine(outputPath, "agents", "planner")));

        var materializationService = SkillTestData.CreateMaterializationService();
        foreach (var skill in skills)
        {
            var materialized = materializationService.Materialize(skill, SkillHostKind.OpenAi);
            Assert.True(materialized.IsSuccess, materialized.Failure?.Message);
            foreach (var file in materialized.Value!.Files)
            {
                var path = Path.Combine(outputPath, "skills", skill.Manifest.SkillName.Value, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                Assert.Equal(file.Content, await File.ReadAllTextAsync(path));
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_Zip_WritesDeterministicSortedEntries ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-agent-export", "zip");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var agent = AgentDistributionTestData.CreateAgent(skills, "planning", "planner", AgentHostKind.OpenAi, "planner.toml");
        var catalog = AgentDistributionTestData.CreateCatalog(skills, [agent]);
        var service = new AgentExportService(SkillTestData.CreateMaterializationService());
        var firstPath = scope.GetPath("first.zip");
        var secondPath = scope.GetPath("second.zip");

        var firstResult = await service.ExportAsync(catalog, AgentHostKind.OpenAi, firstPath, SkillExportFormat.Zip, CancellationToken.None);
        var secondResult = await service.ExportAsync(catalog, AgentHostKind.OpenAi, secondPath, SkillExportFormat.Zip, CancellationToken.None);

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
        using var scope = TestDirectories.CreateTempScope("agent-skills-agent-export", "artifact-collision");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var first = AgentDistributionTestData.CreateAgent(skills, "planning", "planner", AgentHostKind.OpenAi, "shared.toml");
        var second = AgentDistributionTestData.CreateAgent(skills, "quality", "reviewer", AgentHostKind.OpenAi, "shared.toml");
        var catalog = AgentDistributionTestData.CreateCatalog(skills, [second, first]);
        var outputPath = scope.GetPath("exported");
        var service = new AgentExportService(SkillTestData.CreateMaterializationService());

        var result = await service.ExportAsync(catalog, AgentHostKind.OpenAi, outputPath, SkillExportFormat.Directory, CancellationToken.None);

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

        using var scope = TestDirectories.CreateTempScope("agent-skills-agent-export", "symlink-output");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-agent-export", "symlink-output-outside");
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
        var agent = AgentDistributionTestData.CreateAgent(skills, "planning", "planner", AgentHostKind.OpenAi, "planner.toml");
        var service = new AgentExportService(SkillTestData.CreateMaterializationService());

        var result = await service.ExportAsync(
            AgentDistributionTestData.CreateCatalog(skills, [agent]),
            AgentHostKind.OpenAi,
            outputPath,
            SkillExportFormat.Directory,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(outsideScope.FullPath));
    }
}

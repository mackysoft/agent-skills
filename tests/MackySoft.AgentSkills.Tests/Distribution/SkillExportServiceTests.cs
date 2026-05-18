using System.IO.Compression;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Distribution;

public sealed class SkillExportServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_RejectsUnsafePackageName ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "export-unsafe-package");
        var generatedPackage = (await SkillTestData.GenerateFixturePackagesAsync()).First();
        var package = generatedPackage with
        {
            Manifest = generatedPackage.Manifest with
            {
                SkillName = "../escape",
            },
        };
        var service = SkillTestData.CreateExportService();

        var result = await service.ExportAsync([package], OpenAiSkillHostAdapter.HostKey, scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_ZipFormat_RejectsUnsafePackageName ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "export-zip-unsafe-package");
        var generatedPackage = (await SkillTestData.GenerateFixturePackagesAsync()).First();
        var package = generatedPackage with
        {
            Manifest = generatedPackage.Manifest with
            {
                SkillName = "../escape",
            },
        };
        var outputPath = scope.GetPath("release.zip");
        var service = SkillTestData.CreateExportService();

        var result = await service.ExportAsync([package], OpenAiSkillHostAdapter.HostKey, outputPath, SkillExportFormat.Zip, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.False(File.Exists(outputPath));
        Assert.Empty(Directory.EnumerateFiles(scope.FullPath, ".release.zip.*.tmp"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_RejectsExistingSkillDirectoryThatEscapesThroughSymlink ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var outputScope = TestDirectories.CreateTempScope("agent-skills-skills", "export-skill-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "export-skill-symlink-outside");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var symlinkPath = Path.Combine(outputScope.FullPath, packages[0].Manifest.SkillName);
        try
        {
            Directory.CreateSymbolicLink(symlinkPath, outsideScope.FullPath);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var service = SkillTestData.CreateExportService();

        var result = await service.ExportAsync(packages, OpenAiSkillHostAdapter.HostKey, outputScope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_ZipFormat_CleansTemporaryFile_WhenCommitFails ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "export-zip-cleanup");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateExportService();
        var destinationDirectory = scope.CreateDirectory("release.zip");

        var result = await service.ExportAsync(packages, OpenAiSkillHostAdapter.HostKey, destinationDirectory, SkillExportFormat.Zip, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InstallTargetWriteFailed, result.Failure!.Code);
        Assert.Empty(Directory.EnumerateFiles(scope.FullPath, $".{Path.GetFileName(destinationDirectory)}.*.tmp"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExportAsync_ZipFormat_WritesDeterministicEntries ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "export-zip-deterministic");
        var packages = await SkillTestData.GenerateFixturePackagesAsync();
        var service = SkillTestData.CreateExportService();
        var firstZip = scope.GetPath("first.zip");
        var secondZip = scope.GetPath("second.zip");

        var first = await service.ExportAsync(packages, OpenAiSkillHostAdapter.HostKey, firstZip, SkillExportFormat.Zip, CancellationToken.None);
        var second = await service.ExportAsync(packages, OpenAiSkillHostAdapter.HostKey, secondZip, SkillExportFormat.Zip, CancellationToken.None);

        Assert.True(first.IsSuccess, first.Failure?.Message);
        Assert.True(second.IsSuccess, second.Failure?.Message);
        Assert.Equal(await File.ReadAllBytesAsync(firstZip), await File.ReadAllBytesAsync(secondZip));
        using var archive = ZipFile.OpenRead(firstZip);
        var entryNames = archive.Entries.Select(static entry => entry.FullName).ToArray();
        Assert.Equal(entryNames.Order(StringComparer.Ordinal).ToArray(), entryNames);
        Assert.Contains($"{packages[0].Manifest.SkillName}/SKILL.md", entryNames);
        Assert.Contains($"{packages[0].Manifest.SkillName}/agents/openai.yaml", entryNames);
        Assert.All(archive.Entries, static entry => Assert.Equal(new DateTime(1980, 1, 1, 0, 0, 0), entry.LastWriteTime.DateTime));
    }
}

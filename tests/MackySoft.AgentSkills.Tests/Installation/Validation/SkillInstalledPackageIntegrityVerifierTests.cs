using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Installation.Validation;

public sealed class SkillInstalledPackageIntegrityVerifierTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task VerifyAsync_RejectsReferenceDirectorySymlinkWithoutLeakingTargetFilePath ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "integrity-reference-directory-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "integrity-reference-directory-symlink-outside");
        var hostAdapters = SkillTestData.CreateDefaultHostAdapterSet();
        var package = (await SkillTestData.GenerateFixturePackagesAsync()).First();
        var materializedResult = SkillTestData.CreateMaterializationService().Materialize(package, OpenAiSkillHostAdapter.HostKey);
        Assert.True(materializedResult.IsSuccess, materializedResult.Failure?.Message);
        var skillDirectory = scope.CreateDirectory(package.Manifest.SkillName.Value);
        foreach (var file in materializedResult.Value!.Files)
        {
            scope.WriteFile(Path.Combine(package.Manifest.SkillName.Value, file.RelativePath), file.Content);
        }

        const string outsideFileName = "outside-secret.md";
        outsideScope.WriteFile(outsideFileName, "# Outside\n");
        if (!TestSymbolicLinks.TryCreateDirectory(Path.Combine(skillDirectory, "references", "outside"), outsideScope.FullPath))
        {
            return;
        }

        var verifier = SkillTestData.CreateInstalledPackageIntegrityVerifier(hostAdapters);

        var result = await verifier.VerifyAsync(skillDirectory, OpenAiSkillHostAdapter.HostKey, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.PathUnsafe, result.Failure!.Code);
        Assert.Contains("references/outside", result.Failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(outsideFileName, result.Failure.Message, StringComparison.Ordinal);
    }
}

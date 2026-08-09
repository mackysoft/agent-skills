using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Bundles;

public sealed class AgentSkillsBundleSourcePathTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Readers_WhenBundleJsonIsSymbolicLink_ReturnSourceInvalid ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-v2", "linked-descriptor");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-v2", "linked-descriptor-outside");
        var target = outsideScope.WriteFile("bundle.json", CreateBundleJson());
        if (!TryCreateFileSymbolicLink(scope.GetPath("bundle.json"), target))
        {
            return;
        }

        var schemaResult = await new BundleSchemaVersionReader().ReadAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);
        var definitionResult = await new AgentSkillsBundleDefinitionReader(new AgentSkillsBundleJsonSerializer())
            .ReadAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.False(schemaResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, schemaResult.Failure!.Code);
        Assert.False(definitionResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, definitionResult.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Readers_WhenBundleRootIsSymbolicLink_ReturnSourceInvalid ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-skills-v2", "linked-root");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-v2", "linked-root-outside");
        outsideScope.WriteFile("bundle.json", CreateBundleJson());
        var rootLink = scope.GetPath("bundle-root");
        if (!TryCreateDirectorySymbolicLink(rootLink, outsideScope.FullPath))
        {
            return;
        }

        var schemaResult = await new BundleSchemaVersionReader().ReadAsync(AbsolutePath.Parse(rootLink), CancellationToken.None);
        var definitionResult = await new AgentSkillsBundleDefinitionReader(new AgentSkillsBundleJsonSerializer())
            .ReadAsync(AbsolutePath.Parse(rootLink), CancellationToken.None);

        Assert.False(schemaResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, schemaResult.Failure!.Code);
        Assert.False(definitionResult.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, definitionResult.Failure!.Code);
    }

    private static string CreateBundleJson ()
    {
        return """
            {
              "schemaVersion": 2,
              "catalogId": "com.mackysoft.agent-skills.tests",
              "bundleVersion": 1
            }
            """ + "\n";
    }

    private static bool TryCreateFileSymbolicLink (
        string linkPath,
        string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryCreateDirectorySymbolicLink (
        string linkPath,
        string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

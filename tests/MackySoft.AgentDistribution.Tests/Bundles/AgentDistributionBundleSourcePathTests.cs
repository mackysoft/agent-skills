using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Bundles;

public sealed class AgentDistributionBundleSourcePathTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Readers_WhenBundleJsonIsSymbolicLink_ReturnSourceInvalid ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-v3", "linked-descriptor");
        using var outsideScope = TestDirectories.CreateTempScope("agent-distribution-v3", "linked-descriptor-outside");
        var target = outsideScope.WriteFile("bundle.json", CreateBundleJson());
        File.CreateSymbolicLink(scope.GetPath("bundle.json"), target);

        var schemaResult = await new BundleSchemaVersionReader().ReadAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);
        var definitionResult = await new AgentDistributionBundleDefinitionReader(new AgentDistributionBundleJsonSerializer())
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

        using var scope = TestDirectories.CreateTempScope("agent-distribution-v3", "linked-root");
        using var outsideScope = TestDirectories.CreateTempScope("agent-distribution-v3", "linked-root-outside");
        outsideScope.WriteFile("bundle.json", CreateBundleJson());
        var rootLink = scope.GetPath("bundle-root");
        Directory.CreateSymbolicLink(rootLink, outsideScope.FullPath);

        var schemaResult = await new BundleSchemaVersionReader().ReadAsync(AbsolutePath.Parse(rootLink), CancellationToken.None);
        var definitionResult = await new AgentDistributionBundleDefinitionReader(new AgentDistributionBundleJsonSerializer())
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
              "schemaVersion": 3,
              "catalogId": "com.mackysoft.agent-distribution.tests",
              "bundleVersion": 1
            }
            """ + "\n";
    }

}

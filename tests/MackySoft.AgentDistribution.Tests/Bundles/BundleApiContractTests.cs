using System.Reflection;
using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Generation;
using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Sources;

namespace MackySoft.AgentDistribution.Tests.Bundles;

public sealed class BundleApiContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ExternalMutationEntry_IsLimitedToBundleBuildService ()
    {
        Assert.Null(typeof(SkillPackageGenerationService).GetMethod(nameof(SkillPackageGenerationService.GenerateAllAsync)));
        Assert.Null(typeof(SkillPackageGenerationService).GetMethod(nameof(SkillPackageGenerationService.Generate)));
        Assert.Null(typeof(SkillSourceDefinitionReader).GetMethod(nameof(SkillSourceDefinitionReader.ReadAllAsync)));
        Assert.Null(typeof(SkillSourceDefinitionReader).GetMethod(nameof(SkillSourceDefinitionReader.ReadOneAsync)));
        Assert.Null(typeof(CanonicalSkillPackageWriter).GetMethod(nameof(CanonicalSkillPackageWriter.WriteToStagingAsync)));
        Assert.Null(typeof(CanonicalSkillBundleWriter).GetMethod(nameof(CanonicalSkillBundleWriter.WriteAsync)));
        AssertBuildAndReleaseMethods(typeof(SkillBundleBuildService));
        AssertBuildAndReleaseMethods(typeof(AgentDistributionBundleBuildService));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CanonicalModels_CannotBeConstructedOrDeserializedByExternalCallers ()
    {
        Assert.Empty(typeof(SkillManifest).GetConstructors());
        Assert.Empty(typeof(CanonicalSkillPackage).GetConstructors());
        Assert.Empty(typeof(CanonicalSkillBundle).GetConstructors());
        Assert.Null(typeof(SkillManifestJsonSerializer).GetMethod("Deserialize"));
        Assert.Null(typeof(SkillManifestJsonSerializer).GetMethod("TryDeserialize"));
        Assert.False(typeof(SkillSourceDefinition).IsPublic);
        Assert.False(typeof(SkillSourceMetadata).IsPublic);
        Assert.False(typeof(SkillSourceReference).IsPublic);
    }

    private static void AssertBuildAndReleaseMethods (Type serviceType)
    {
        var serviceMethods = serviceType.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(static method => method.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, serviceMethods.Length);
        var buildServiceMethod = serviceMethods[0];
        Assert.Equal(nameof(SkillBundleBuildService.BuildAsync), buildServiceMethod.Name);
        Assert.Equal(
            [typeof(string), typeof(bool), typeof(CancellationToken)],
            buildServiceMethod.GetParameters().Select(static parameter => parameter.ParameterType));
        var releaseServiceMethod = serviceMethods[1];
        Assert.Equal(nameof(SkillBundleBuildService.PrepareReleaseAsync), releaseServiceMethod.Name);
        Assert.Equal(
            [typeof(string), typeof(int), typeof(bool), typeof(CancellationToken)],
            releaseServiceMethod.GetParameters().Select(static parameter => parameter.ParameterType));
    }
}

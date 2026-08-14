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
    public void ExternalMutationEntry_ExposesOnlyVersionPreservingBuildService ()
    {
        Assert.Null(typeof(SkillPackageGenerationService).GetMethod(nameof(SkillPackageGenerationService.GenerateAllAsync)));
        Assert.Null(typeof(SkillPackageGenerationService).GetMethod(nameof(SkillPackageGenerationService.Generate)));
        Assert.Null(typeof(SkillSourceDefinitionReader).GetMethod(nameof(SkillSourceDefinitionReader.ReadAllAsync)));
        Assert.Null(typeof(SkillSourceDefinitionReader).GetMethod(nameof(SkillSourceDefinitionReader.ReadOneAsync)));
        Assert.Null(typeof(CanonicalSkillPackageWriter).GetMethod(nameof(CanonicalSkillPackageWriter.WriteToStagingAsync)));
        Assert.Null(typeof(CanonicalSkillBundleWriter).GetMethod(nameof(CanonicalSkillBundleWriter.WriteAsync)));
        AssertBuildMethod(typeof(SkillBundleBuildService));
        AssertBuildMethod(typeof(AgentDistributionBundleBuildService));
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

    private static void AssertBuildMethod (Type serviceType)
    {
        var serviceMethods = serviceType.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(static method => method.Name, StringComparer.Ordinal)
            .ToArray();
        var buildServiceMethod = Assert.Single(serviceMethods);
        Assert.Equal(nameof(SkillBundleBuildService.BuildAsync), buildServiceMethod.Name);
        Assert.Equal(
            [typeof(AbsolutePath), typeof(AbsolutePath), typeof(bool), typeof(CancellationToken)],
            buildServiceMethod.GetParameters().Select(static parameter => parameter.ParameterType));
    }
}

using System.Reflection;
using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Generation;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Sources;

namespace MackySoft.AgentSkills.Tests.Bundles;

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
        var buildServiceMethods = typeof(SkillBundleBuildService).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.NotEmpty(buildServiceMethods);
        Assert.All(
            buildServiceMethods,
            static method => Assert.Equal(nameof(SkillBundleBuildService.BuildAsync), method.Name));
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
}

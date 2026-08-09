using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Bundles;

public sealed class SkillBundleDefinitionReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ReadsCanonicalAuthoredFieldsWithoutGeneratedDigest ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "source-bundle");
        var serializer = new SkillBundleJsonSerializer();
        var definition = new SkillBundleDefinition(
            SkillBundleDefinition.CurrentSchemaVersion,
            new SkillCatalogId("com.mackysoft.agent-distribution"),
            new SkillBundleVersion(3));
        scope.WriteFile("bundle.json", serializer.SerializeDefinition(definition));
        var reader = CreateReader(serializer);

        var result = await reader.ReadAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.NotNull(result.Value);
        Assert.Equal(definition.SchemaVersion, result.Value.SchemaVersion);
        Assert.Equal(definition.CatalogId, result.Value.CatalogId);
        Assert.Equal(definition.SkillBundleVersion, result.Value.SkillBundleVersion);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_RejectsGeneratedDigestInAuthoredBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "source-bundle-generated-field");
        var serializer = new SkillBundleJsonSerializer();
        var definition = new SkillBundleDefinition(
            SkillBundleDefinition.CurrentSchemaVersion,
            new SkillCatalogId("com.mackysoft.agent-distribution"),
            new SkillBundleVersion(3));
        scope.WriteFile(
            "bundle.json",
            serializer.SerializeDescriptor(new SkillBundleDescriptor(
                definition.SchemaVersion,
                definition.CatalogId,
                definition.SkillBundleVersion,
                Sha256Digest.Parse(new string('a', 64)))));
        var reader = CreateReader(serializer);

        var result = await reader.ReadAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
        Assert.Contains("canonical", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_RejectsBundleFileSymlinkOutsideBundleRoot ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "source-bundle-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-distribution-skills", "source-bundle-symlink-outside");
        var serializer = new SkillBundleJsonSerializer();
        var definition = new SkillBundleDefinition(
            SkillBundleDefinition.CurrentSchemaVersion,
            new SkillCatalogId("com.mackysoft.agent-distribution"),
            new SkillBundleVersion(3));
        var outsideBundlePath = outsideScope.WriteFile("bundle.json", serializer.SerializeDefinition(definition));
        File.CreateSymbolicLink(scope.GetPath("bundle.json"), outsideBundlePath);

        var reader = CreateReader(serializer);

        var result = await reader.ReadAsync(AbsolutePath.Parse(scope.FullPath), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    private static SkillBundleDefinitionReader CreateReader (SkillBundleJsonSerializer serializer)
    {
        return new SkillBundleDefinitionReader(serializer);
    }

}

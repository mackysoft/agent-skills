using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Bundles;

public sealed class SkillBundleDefinitionReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_ReadsCanonicalAuthoredFieldsWithoutGeneratedDigest ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "source-bundle");
        var serializer = new SkillBundleJsonSerializer();
        var definition = new SkillBundleDefinition(
            SkillBundleDefinition.CurrentSchemaVersion,
            new SkillCatalogId("com.mackysoft.agent-skills"),
            new SkillBundleVersion(3));
        scope.WriteFile("bundle.json", serializer.SerializeDefinition(definition));
        var reader = CreateReader(serializer);

        var result = await reader.ReadAsync(scope.FullPath, CancellationToken.None);

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
        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "source-bundle-generated-field");
        var serializer = new SkillBundleJsonSerializer();
        var definition = new SkillBundleDefinition(
            SkillBundleDefinition.CurrentSchemaVersion,
            new SkillCatalogId("com.mackysoft.agent-skills"),
            new SkillBundleVersion(3));
        scope.WriteFile(
            "bundle.json",
            serializer.SerializeDescriptor(new SkillBundleDescriptor(
                definition.SchemaVersion,
                definition.CatalogId,
                definition.SkillBundleVersion,
                Sha256Digest.Parse(new string('a', 64)))));
        var reader = CreateReader(serializer);

        var result = await reader.ReadAsync(scope.FullPath, CancellationToken.None);

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

        using var scope = TestDirectories.CreateTempScope("agent-skills-skills", "source-bundle-symlink");
        using var outsideScope = TestDirectories.CreateTempScope("agent-skills-skills", "source-bundle-symlink-outside");
        var serializer = new SkillBundleJsonSerializer();
        var definition = new SkillBundleDefinition(
            SkillBundleDefinition.CurrentSchemaVersion,
            new SkillCatalogId("com.mackysoft.agent-skills"),
            new SkillBundleVersion(3));
        var outsideBundlePath = outsideScope.WriteFile("bundle.json", serializer.SerializeDefinition(definition));
        if (!TryCreateFileSymbolicLink(scope.GetPath("bundle.json"), outsideBundlePath))
        {
            return;
        }

        var reader = CreateReader(serializer);

        var result = await reader.ReadAsync(scope.FullPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }

    private static SkillBundleDefinitionReader CreateReader (SkillBundleJsonSerializer serializer)
    {
        return new SkillBundleDefinitionReader(serializer);
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
}

using System.Text.Json;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Tests.Manifests;

public sealed class SkillManifestJsonSerializerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Serialize_UsesLfLineEndings ()
    {
        var serializer = new SkillManifestJsonSerializer();
        var manifest = CreateManifest();

        var json = serializer.Serialize(manifest);

        Assert.DoesNotContain("\r\n", json, StringComparison.Ordinal);
        Assert.EndsWith("\n", json, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Serialize_WritesManifestDigestInCanonicalOrder ()
    {
        var serializer = new SkillManifestJsonSerializer();
        var manifest = CreateManifest();

        var json = serializer.Serialize(manifest);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(64, document.RootElement.GetProperty("manifestDigest").GetString()?.Length);
        Assert.Equal(
            new[] { "schemaVersion", "catalogId", "tier", "contentDigest", "manifestDigest", "skillName", "displayName", "description", "dependencies", "hostArtifacts" },
            document.RootElement.EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.Equal(
            new[] { "a-helper", "z-helper" },
            document.RootElement.GetProperty("dependencies").EnumerateArray().Select(static dependency => dependency.GetString()).ToArray());
        Assert.Equal(
            new[] { "claude", "copilot", "openai" },
            document.RootElement.GetProperty("hostArtifacts").EnumerateArray().Select(static artifact => artifact.GetProperty("host").GetString()).ToArray());
        Assert.Equal(
            new[] { "host", "path", "digest", "materializedFrontmatterDigest" },
            document.RootElement.GetProperty("hostArtifacts").EnumerateArray().Last().EnumerateObject().Select(static property => property.Name).ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("""
        {
          "schemaVersion": 1,
          "skillName": "sample-skill",
          "displayName": "Sample Skill",
          "description": "Use this sample skill for tests.",
          "tier": "basic",
          "catalogId": "com.mackysoft.agent-skills",
          "contentDigest": "0000000000000000000000000000000000000000000000000000000000000000",
          "hostArtifacts": []
        }
        """)]
    [Trait("Size", "Small")]
    public void TryDeserialize_ReturnsManifestInvalid_WhenJsonIsMalformedOrIncomplete (string json)
    {
        var serializer = new SkillManifestJsonSerializer();

        var result = serializer.TryDeserialize(json);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    private static SkillManifest CreateManifest ()
    {
        var serializer = new SkillManifestJsonSerializer();
        var digestCalculator = new SkillManifestDigestCalculator(serializer);
        var manifest = new SkillManifest(
            SkillManifest.CurrentSchemaVersion,
            new SkillName("sample-skill"),
            "Sample Skill",
            "Use this sample skill for tests.",
            [new SkillName("z-helper"), new SkillName("a-helper")],
            new SkillTier("basic"),
            new SkillCatalogId("com.mackysoft.agent-skills"),
            new string('0', 64),
            string.Empty,
            [
                new SkillHostArtifactManifest("openai", "agents/openai.yaml", new string('1', 64), new string('2', 64)),
                new SkillHostArtifactManifest("claude", null, null, new string('3', 64)),
                new SkillHostArtifactManifest("copilot", null, null, new string('4', 64)),
            ]);

        return digestCalculator.WithComputedManifestDigest(manifest);
    }
}

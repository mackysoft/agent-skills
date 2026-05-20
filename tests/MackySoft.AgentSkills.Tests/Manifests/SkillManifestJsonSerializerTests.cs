using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Shared;

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

        Assert.Contains("\"manifestDigest\": \"sha256:", json, StringComparison.Ordinal);
        Assert.True(
            json.IndexOf("\"contentDigest\"", StringComparison.Ordinal) < json.IndexOf("\"manifestDigest\"", StringComparison.Ordinal),
            json);
        Assert.True(
            json.IndexOf("\"manifestDigest\"", StringComparison.Ordinal) < json.IndexOf("\"hostArtifacts\"", StringComparison.Ordinal),
            json);
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
          "contentDigest": "sha256:0000000000000000000000000000000000000000000000000000000000000000",
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
        var digestCalculator = new SkillManifestDigestCalculator(new SkillDigestCalculator(), serializer);
        var manifest = new SkillManifest(
            SkillManifest.CurrentSchemaVersion,
            "sample-skill",
            "Sample Skill",
            "Use this sample skill for tests.",
            "sha256:" + new string('0', 64),
            string.Empty,
            [
                new SkillHostArtifactManifest("openai", "agents/openai.yaml", "sha256:" + new string('1', 64), "sha256:" + new string('2', 64)),
                new SkillHostArtifactManifest("claude", null, null, "sha256:" + new string('3', 64)),
                new SkillHostArtifactManifest("copilot", null, null, "sha256:" + new string('4', 64)),
            ]);

        return digestCalculator.WithComputedManifestDigest(manifest);
    }
}

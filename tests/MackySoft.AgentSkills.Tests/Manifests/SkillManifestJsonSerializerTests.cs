using System.Text.Json;
using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Names;
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

        using var document = JsonDocument.Parse(json);
        Assert.Equal(64, document.RootElement.GetProperty("manifestDigest").GetString()?.Length);
        Assert.Equal(
            new[] { "schemaVersion", "skillBundleVersion", "catalogId", "category", "skillName", "displayName", "description", "dependencies", "contentDigest", "manifestDigest", "hostArtifacts" },
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
    [InlineData("{\"schemaVersion\":0}")]
    [InlineData("""
        {
          "schemaVersion": 1,
          "skillName": "sample-skill",
          "displayName": "Sample Skill",
          "description": "Use this sample skill for tests.",
          "category": "core",
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

    [Fact]
    [Trait("Size", "Small")]
    public void TryDeserialize_RejectsCurrentSchemaManifestWithoutRequiredFields ()
    {
        var serializer = new SkillManifestJsonSerializer();

        var result = serializer.TryDeserialize("""
            {
              "schemaVersion": 1,
              "catalogId": "com.mackysoft.agent-skills",
              "category": "core",
              "contentDigest": "0000000000000000000000000000000000000000000000000000000000000000",
              "manifestDigest": "1111111111111111111111111111111111111111111111111111111111111111",
              "skillName": "sample-skill",
              "displayName": "Sample Skill",
              "description": "Use this sample skill for tests.",
              "hostArtifacts": [
                {
                  "host": "claude",
                  "materializedFrontmatterDigest": "2222222222222222222222222222222222222222222222222222222222222222"
                }
              ]
            }
            """);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    private static SkillManifest CreateManifest ()
    {
        var serializer = new SkillManifestJsonSerializer();
        var manifest = new SkillManifestCandidate(
            SkillManifest.CurrentSchemaVersion,
            new SkillBundleVersion(1),
            new SkillCatalogId("com.mackysoft.agent-skills"),
            new SkillCategory("core"),
            new SkillName("sample-skill"),
            "Sample Skill",
            "Use this sample skill for tests.",
            [new SkillName("z-helper"), new SkillName("a-helper")],
            Digest('0'),
            Digest('f'),
            [
                new SkillHostArtifactManifest(SkillHostKind.OpenAi, "agents/openai.yaml", Digest('1'), Digest('2')),
                new SkillHostArtifactManifest(SkillHostKind.Claude, null, null, Digest('3')),
                new SkillHostArtifactManifest(SkillHostKind.Copilot, null, null, Digest('4')),
            ]);

        return SkillTestData.WithComputedManifestDigest(manifest);
    }

    private static Sha256Digest Digest (char value)
    {
        return Sha256Digest.Parse(new string(value, 64));
    }
}

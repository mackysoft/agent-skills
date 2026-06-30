using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Installation.Validation;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Tests.Installation.Validation;

public sealed class SkillInstalledManifestLegacyCompatibilitySerializerTests
{
    private const string ExpectedDigest = "9b945765a48672f2e9ded748598bf13d186a0edcde024d673b0d79440228ff86";

    [Fact]
    [Trait("Size", "Small")]
    public void SerializeSchemaVersionOneWithoutDependencies_WritesLegacyManifestShape ()
    {
        var serializer = new SkillInstalledManifestLegacyCompatibilitySerializer();
        var manifest = CreateManifest() with
        {
            ManifestDigest = ExpectedDigest,
        };

        var json = serializer.SerializeSchemaVersionOneWithoutDependencies(manifest);

        Assert.Equal(string.Join('\n', [
            "{",
            "  \"schemaVersion\": 1,",
            "  \"catalogId\": \"com.mackysoft.agent-skills\",",
            "  \"tier\": \"basic\",",
            "  \"contentDigest\": \"0000000000000000000000000000000000000000000000000000000000000000\",",
            $"  \"manifestDigest\": \"{ExpectedDigest}\",",
            "  \"skillName\": \"sample-skill\",",
            "  \"displayName\": \"Sample Skill\",",
            "  \"description\": \"Use this sample skill for tests.\",",
            "  \"hostArtifacts\": [",
            "    {",
            "      \"host\": \"claude\",",
            "      \"materializedFrontmatterDigest\": \"1111111111111111111111111111111111111111111111111111111111111111\"",
            "    },",
            "    {",
            "      \"host\": \"copilot\",",
            "      \"materializedFrontmatterDigest\": \"2222222222222222222222222222222222222222222222222222222222222222\"",
            "    },",
            "    {",
            "      \"host\": \"openai\",",
            "      \"path\": \"agents/openai.yaml\",",
            "      \"digest\": \"3333333333333333333333333333333333333333333333333333333333333333\",",
            "      \"materializedFrontmatterDigest\": \"4444444444444444444444444444444444444444444444444444444444444444\"",
            "    }",
            "  ]",
            "}",
        ]) + "\n", json);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ComputeSchemaVersionOneWithoutDependenciesManifestDigest_UsesLegacyManifestShape ()
    {
        var serializer = new SkillInstalledManifestLegacyCompatibilitySerializer();

        var digest = serializer.ComputeSchemaVersionOneWithoutDependenciesManifestDigest(CreateManifest());

        Assert.Equal(ExpectedDigest, digest);
    }

    private static SkillManifest CreateManifest ()
    {
        return new SkillManifest(
            SkillManifest.CurrentSchemaVersion,
            new SkillName("sample-skill"),
            "Sample Skill",
            "Use this sample skill for tests.",
            [new SkillName("dependency-skill")],
            new SkillTier("basic"),
            new SkillCatalogId("com.mackysoft.agent-skills"),
            new string('0', 64),
            new string('f', 64),
            [
                new SkillHostArtifactManifest("openai", "agents/openai.yaml", new string('3', 64), new string('4', 64)),
                new SkillHostArtifactManifest("claude", null, null, new string('1', 64)),
                new SkillHostArtifactManifest("copilot", null, null, new string('2', 64)),
            ]);
    }
}

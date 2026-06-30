using System.Security.Cryptography;
using System.Text;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Tests.Manifests;

public sealed class SkillManifestDigestCalculatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ComputeManifestDigest_UsesCanonicalManifestJsonUtf8BytesWithoutManifestDigest ()
    {
        var expectedDigestInput = string.Join('\n', [
            "{",
            "  \"schemaVersion\": 1,",
            "  \"skillBundleVersion\": 1,",
            "  \"catalogId\": \"com.mackysoft.agent-skills\",",
            "  \"tier\": \"basic\",",
            "  \"skillName\": \"sample-skill\",",
            "  \"displayName\": \"Sample Skill\",",
            "  \"description\": \"Use this sample skill for tests.\",",
            "  \"contentDigest\": \"0000000000000000000000000000000000000000000000000000000000000000\",",
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
        ]) + "\n";
        var manifest = new SkillManifest(
            SkillManifest.CurrentSchemaVersion,
            1,
            new SkillCatalogId("com.mackysoft.agent-skills"),
            new SkillTier("basic"),
            "sample-skill",
            "Sample Skill",
            "Use this sample skill for tests.",
            new string('0', 64),
            new string('f', 64),
            [
                new SkillHostArtifactManifest("openai", "agents/openai.yaml", new string('3', 64), new string('4', 64)),
                new SkillHostArtifactManifest("claude", null, null, new string('1', 64)),
                new SkillHostArtifactManifest("copilot", null, null, new string('2', 64)),
            ]);
        var calculator = new SkillManifestDigestCalculator(new SkillManifestJsonSerializer());
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedDigestInput));
        var expectedDigest = Convert.ToHexString(expectedHash).ToLowerInvariant();

        var actualDigest = calculator.ComputeManifestDigest(manifest);

        Assert.Equal(expectedDigest, actualDigest);
        Assert.Equal(actualDigest.ToLowerInvariant(), actualDigest);
    }
}

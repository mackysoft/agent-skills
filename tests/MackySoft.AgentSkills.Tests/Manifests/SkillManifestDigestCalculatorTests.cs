using System.Security.Cryptography;
using System.Text;
using MackySoft.AgentSkills.Manifests;

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
            "  \"skillName\": \"sample-skill\",",
            "  \"displayName\": \"Sample Skill\",",
            "  \"description\": \"Use this sample skill for tests.\",",
            "  \"contentDigest\": \"sha256:0000000000000000000000000000000000000000000000000000000000000000\",",
            "  \"hostArtifacts\": [",
            "    {",
            "      \"host\": \"claude\",",
            "      \"materializedFrontmatterDigest\": \"sha256:1111111111111111111111111111111111111111111111111111111111111111\"",
            "    },",
            "    {",
            "      \"host\": \"copilot\",",
            "      \"materializedFrontmatterDigest\": \"sha256:2222222222222222222222222222222222222222222222222222222222222222\"",
            "    },",
            "    {",
            "      \"host\": \"openai\",",
            "      \"path\": \"agents/openai.yaml\",",
            "      \"digest\": \"sha256:3333333333333333333333333333333333333333333333333333333333333333\",",
            "      \"materializedFrontmatterDigest\": \"sha256:4444444444444444444444444444444444444444444444444444444444444444\"",
            "    }",
            "  ]",
            "}",
        ]) + "\n";
        var manifest = new SkillManifest(
            SkillManifest.CurrentSchemaVersion,
            "sample-skill",
            "Sample Skill",
            "Use this sample skill for tests.",
            "sha256:" + new string('0', 64),
            "sha256:" + new string('f', 64),
            [
                new SkillHostArtifactManifest("openai", "agents/openai.yaml", "sha256:" + new string('3', 64), "sha256:" + new string('4', 64)),
                new SkillHostArtifactManifest("claude", null, null, "sha256:" + new string('1', 64)),
                new SkillHostArtifactManifest("copilot", null, null, "sha256:" + new string('2', 64)),
            ]);
        var calculator = new SkillManifestDigestCalculator(new SkillManifestJsonSerializer());
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedDigestInput));
        var expectedDigest = "sha256:" + Convert.ToHexString(expectedHash).ToLowerInvariant();

        var actualDigest = calculator.ComputeManifestDigest(manifest);

        Assert.Equal(expectedDigest, actualDigest);
        Assert.Equal(actualDigest.ToLowerInvariant(), actualDigest);
    }
}

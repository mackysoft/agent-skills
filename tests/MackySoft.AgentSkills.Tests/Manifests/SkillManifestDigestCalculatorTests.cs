using System.Security.Cryptography;
using System.Text;
using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Names;

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
            "  \"category\": \"core\",",
            "  \"skillName\": \"sample-skill\",",
            "  \"displayName\": \"Sample Skill\",",
            "  \"description\": \"Use this sample skill for tests.\",",
            "  \"dependencies\": [",
            "    \"a-helper\",",
            "    \"z-helper\"",
            "  ],",
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
                new SkillHostArtifactManifest(SkillHostKind.OpenAi, "agents/openai.yaml", Digest('3'), Digest('4')),
                new SkillHostArtifactManifest(SkillHostKind.Claude, null, null, Digest('1')),
                new SkillHostArtifactManifest(SkillHostKind.Copilot, null, null, Digest('2')),
            ]);
        var calculator = new SkillManifestDigestCalculator(new SkillManifestJsonSerializer());
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedDigestInput));
        var expectedDigest = Convert.ToHexString(expectedHash).ToLowerInvariant();

        var actualDigest = calculator.ComputeManifestDigest(manifest);

        Assert.Equal(expectedDigest, actualDigest.ToString());
        Assert.Equal(actualDigest.ToString().ToLowerInvariant(), actualDigest.ToString());
    }

    private static Sha256Digest Digest (char value)
    {
        return Sha256Digest.Parse(new string(value, 64));
    }
}

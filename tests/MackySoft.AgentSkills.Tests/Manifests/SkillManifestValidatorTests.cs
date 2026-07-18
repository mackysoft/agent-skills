using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Manifests;

public sealed class SkillManifestFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Validate_AcceptsSafeSkillName ()
    {
        var factory = SkillTestData.CreateManifestFactory();

        var result = factory.CreateCanonical(CreateCandidate("sample-skill"));

        Assert.True(result.IsSuccess, result.Failure?.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SampleSkill")]
    [InlineData("../escape")]
    [InlineData("sample/skill")]
    [InlineData(".")]
    [InlineData("-sample")]
    [Trait("Size", "Small")]
    public void SkillName_RejectsUnsafeLiteral (string skillName)
    {
        Assert.Throws<ArgumentException>(() => new SkillName(skillName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_RejectsManifestDigestDrift ()
    {
        var factory = SkillTestData.CreateManifestFactory();
        var manifest = SkillTestData.CopyManifest(
            CreateManifest("sample-skill"),
            displayName: "Drifted Skill");

        var result = factory.CreateCanonical(manifest);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("manifestDigest", result.Failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(InvalidManifestCases))]
    [Trait("Size", "Small")]
    public void Validate_RejectsInvalidManifestShape (object value)
    {
        var factory = SkillTestData.CreateManifestFactory();
        var manifest = Assert.IsType<SkillManifestCandidate>(value);

        var result = factory.CreateCanonical(manifest);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    public static TheoryData<object> InvalidManifestCases ()
    {
        var valid = CreateManifest("sample-skill");
        return new TheoryData<object>
        {
            SkillTestData.CopyManifest(valid, hostArtifacts: valid.HostArtifacts.Where(static artifact => artifact.Host != SkillHostKind.Copilot).ToArray()),
            SkillTestData.CopyManifest(valid, hostArtifacts: valid.HostArtifacts.Select(static artifact => artifact.Host == SkillHostKind.Claude ? new SkillHostArtifactManifest(artifact.Host, "claude.yaml", Digest('6'), artifact.MaterializedFrontmatterDigest) : artifact).ToArray()),
            SkillTestData.CopyManifest(valid, hostArtifacts: valid.HostArtifacts.Select(static artifact => artifact.Host == SkillHostKind.OpenAi ? new SkillHostArtifactManifest(artifact.Host, "agents/other.yaml", artifact.Digest, artifact.MaterializedFrontmatterDigest) : artifact).ToArray()),
        };
    }

    private static SkillManifest CreateManifest (string skillName)
    {
        var result = SkillTestData.CreateManifestFactory().CreateCanonical(CreateCandidate(skillName));
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }

    private static SkillManifestCandidate CreateCandidate (string skillName)
    {
        return new SkillManifestCandidate(
            SkillManifest.CurrentSchemaVersion,
            new SkillBundleVersion(1),
            new SkillCatalogId("com.mackysoft.agent-skills"),
            new SkillCategory("core"),
            new SkillName(skillName),
            "Sample Skill",
            "Use this sample skill for tests.",
            [],
            Digest('0'),
            null,
            [
                new SkillHostArtifactManifest(SkillHostKind.Claude, null, null, Digest('1')),
                new SkillHostArtifactManifest(SkillHostKind.Copilot, null, null, Digest('2')),
                new SkillHostArtifactManifest(SkillHostKind.OpenAi, "agents/openai.yaml", Digest('3'), Digest('4')),
            ]);

    }

    private static Sha256Digest Digest (char value)
    {
        return Sha256Digest.Parse(new string(value, 64));
    }
}

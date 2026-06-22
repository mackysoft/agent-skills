using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Tests.Manifests;

public sealed class SkillManifestValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Validate_AcceptsSafeSkillName ()
    {
        var validator = SkillTestData.CreateManifestValidator();

        var result = validator.Validate(CreateManifest("sample-skill"));

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
    public void Validate_RejectsUnsafeSkillName (string skillName)
    {
        var validator = SkillTestData.CreateManifestValidator();

        var result = validator.Validate(CreateManifest(skillName));

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_RejectsManifestDigestDrift ()
    {
        var validator = SkillTestData.CreateManifestValidator();
        var manifest = CreateManifest("sample-skill") with
        {
            DisplayName = "Drifted Skill",
        };

        var result = validator.Validate(manifest);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("manifestDigest", result.Failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(InvalidManifestCases))]
    [Trait("Size", "Small")]
    public void Validate_RejectsInvalidManifestShape (SkillManifest manifest)
    {
        var validator = SkillTestData.CreateManifestValidator();

        var result = validator.Validate(manifest);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    public static TheoryData<SkillManifest> InvalidManifestCases ()
    {
        var valid = CreateManifest("sample-skill");
        return new TheoryData<SkillManifest>
        {
            WithComputedManifestDigest(valid with { SchemaVersion = 0 }),
            WithComputedManifestDigest(valid with { DisplayName = "" }),
            WithComputedManifestDigest(valid with { Description = "" }),
            valid with { Tier = null! },
            WithComputedManifestDigest(valid with { ContentDigest = "not-hex" }),
            valid with { ManifestDigest = "not-hex" },
            WithComputedManifestDigest(valid with { HostArtifacts = valid.HostArtifacts.Where(static artifact => artifact.Host != "copilot").ToArray() }),
            WithComputedManifestDigest(valid with { HostArtifacts = valid.HostArtifacts.Concat([new SkillHostArtifactManifest("generic", null, null, new string('5', 64))]).ToArray() }),
            WithComputedManifestDigest(valid with { HostArtifacts = valid.HostArtifacts.Select(static artifact => artifact.Host == "claude" ? artifact with { MaterializedFrontmatterDigest = "not-hex" } : artifact).ToArray() }),
            WithComputedManifestDigest(valid with { HostArtifacts = valid.HostArtifacts.Select(static artifact => artifact.Host == "claude" ? artifact with { Path = "claude.yaml", Digest = new string('6', 64) } : artifact).ToArray() }),
            WithComputedManifestDigest(valid with { HostArtifacts = valid.HostArtifacts.Select(static artifact => artifact.Host == "openai" ? artifact with { Path = "agents/other.yaml" } : artifact).ToArray() }),
            WithComputedManifestDigest(valid with { HostArtifacts = valid.HostArtifacts.Select(static artifact => artifact.Host == "openai" ? artifact with { Digest = null } : artifact).ToArray() }),
        };
    }

    private static SkillManifest CreateManifest (string skillName)
    {
        var serializer = new SkillManifestJsonSerializer();
        var manifest = new SkillManifest(
            SkillManifest.CurrentSchemaVersion,
            skillName,
            "Sample Skill",
            "Use this sample skill for tests.",
            new SkillTier("basic"),
            new string('0', 64),
            string.Empty,
            [
                new SkillHostArtifactManifest("claude", null, null, new string('1', 64)),
                new SkillHostArtifactManifest("copilot", null, null, new string('2', 64)),
                new SkillHostArtifactManifest("openai", "agents/openai.yaml", new string('3', 64), new string('4', 64)),
            ]);

        return WithComputedManifestDigest(manifest);
    }

    private static SkillManifest WithComputedManifestDigest (SkillManifest manifest)
    {
        return new SkillManifestDigestCalculator(new SkillManifestJsonSerializer())
            .WithComputedManifestDigest(manifest);
    }
}

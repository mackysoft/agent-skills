using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Manifests;

namespace MackySoft.AgentDistribution.Tests.Manifests;

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
        Assert.Equal(AgentDistributionFailureCodes.ManifestInvalid, result.Failure!.Code);
        Assert.Contains("manifestDigest", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_RejectsMissingSupportedHostArtifact ()
    {
        var factory = SkillTestData.CreateManifestFactory();
        var valid = CreateManifest("sample-skill");
        var manifest = SkillTestData.CopyManifest(
            valid,
            hostArtifacts: valid.HostArtifacts
                .Where(static artifact => artifact.Host != HostKind.GitHubCopilot)
                .ToArray());

        var result = factory.CreateCanonical(manifest);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_AcceptsHostArtifactFileShapeWithoutCurrentRegistrationPolicy ()
    {
        var candidate = CreateCandidate("sample-skill");
        var hostArtifacts = candidate.HostArtifacts
            .Select(static artifact => artifact.Host == HostKind.ClaudeCode
                ? new SkillHostArtifactManifest(
                    artifact.Host,
                    PackageRelativePath.Parse("claude.yaml"),
                    Digest('6'),
                    artifact.MaterializedFrontmatterDigest)
                : artifact)
            .ToArray();
        var registrationIndependentCandidate = new SkillManifestCandidate(
            candidate.SchemaVersion,
            candidate.SkillBundleVersion,
            candidate.CatalogId,
            candidate.Category,
            candidate.SkillName,
            candidate.DisplayName,
            candidate.Description,
            candidate.Dependencies,
            candidate.ContentDigest,
            manifestDigest: null,
            hostArtifacts);
        var factory = SkillTestData.CreateManifestFactory();

        var result = factory.CreateCanonical(registrationIndependentCandidate);

        Assert.True(result.IsSuccess, result.Failure?.Message);
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
            new AgentDistributionCatalogId("com.mackysoft.agent-distribution"),
            new SkillCategory("core"),
            new SkillName(skillName),
            "Sample Skill",
            "Use this sample skill for tests.",
            [],
            Digest('0'),
            null,
            [
                new SkillHostArtifactManifest(HostKind.ClaudeCode, null, null, Digest('1')),
                new SkillHostArtifactManifest(HostKind.GitHubCopilot, null, null, Digest('2')),
                new SkillHostArtifactManifest(HostKind.Codex, PackageRelativePath.Parse("agents/openai.yaml"), Digest('3'), Digest('4')),
            ]);

    }

    private static Sha256Digest Digest (char value)
    {
        return Sha256Digest.Parse(new string(value, 64));
    }
}

using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Names;

namespace MackySoft.AgentSkills.Tests.Manifests;

public sealed class SkillManifestCandidateTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0, "Sample Skill", "Use this sample skill for tests.")]
    [InlineData(1, "", "Use this sample skill for tests.")]
    [InlineData(1, "Sample Skill", "")]
    public void Candidate_RejectsInvalidScalarContract (
        int schemaVersion,
        string displayName,
        string description)
    {
        Assert.ThrowsAny<ArgumentException>(() => CreateManifest(
            schemaVersion,
            new SkillName("sample-skill"),
            displayName,
            description,
            [],
            []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Candidate_RejectsInvalidDependencies ()
    {
        Assert.Throws<ArgumentException>(() => CreateManifest(
            SkillManifest.CurrentSchemaVersion,
            new SkillName("sample-skill"),
            "Sample Skill",
            "Use this sample skill for tests.",
            [null!],
            []));
        Assert.Throws<ArgumentException>(() => CreateManifest(
            SkillManifest.CurrentSchemaVersion,
            new SkillName("sample-skill"),
            "Sample Skill",
            "Use this sample skill for tests.",
            [new SkillName("sample-skill")],
            []));
        Assert.Throws<ArgumentException>(() => CreateManifest(
            SkillManifest.CurrentSchemaVersion,
            new SkillName("sample-skill"),
            "Sample Skill",
            "Use this sample skill for tests.",
            [new SkillName("sample-helper"), new SkillName("sample-helper")],
            []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Candidate_CapturesImmutableCollectionSnapshots ()
    {
        var dependencies = new List<SkillName> { new("sample-helper") };
        var hostArtifacts = new List<SkillHostArtifactManifest>
        {
            new(SkillHostKind.Claude, null, null, Digest('1')),
        };
        var manifest = CreateManifest(dependencies, hostArtifacts);

        dependencies[0] = new SkillName("other-helper");
        hostArtifacts[0] = new SkillHostArtifactManifest(SkillHostKind.Copilot, null, null, Digest('2'));

        Assert.Equal("sample-helper", Assert.Single(manifest.Dependencies).Value);
        Assert.Equal(SkillHostKind.Claude, Assert.Single(manifest.HostArtifacts).Host);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Candidate_RejectsNullContentDigest ()
    {
        Assert.Throws<ArgumentNullException>(() => new SkillManifestCandidate(
            SkillManifest.CurrentSchemaVersion,
            new SkillBundleVersion(1),
            new SkillCatalogId("com.mackysoft.agent-skills"),
            new SkillCategory("core"),
            new SkillName("sample-skill"),
            "Sample Skill",
            "Use this sample skill for tests.",
            [],
            null!,
            Digest('f'),
            []));

        Assert.Throws<ArgumentNullException>(() => new SkillHostArtifactManifest(SkillHostKind.Claude, null, null, null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HostArtifactConstructor_RejectsUnpairedPathAndDigest ()
    {
        Assert.Throws<ArgumentException>(() => new SkillHostArtifactManifest(SkillHostKind.OpenAi, "agents/openai.yaml", null, Digest('1')));
        Assert.Throws<ArgumentException>(() => new SkillHostArtifactManifest(SkillHostKind.OpenAi, null, Digest('2'), Digest('1')));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void HostArtifactConstructor_RejectsUndefinedHost ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillHostArtifactManifest((SkillHostKind)42, null, null, Digest('1')));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("../openai.yaml")]
    [InlineData("/agents/openai.yaml")]
    public void HostArtifactConstructor_RejectsUnsafeArtifactPath (string path)
    {
        Assert.Throws<ArgumentException>(() => new SkillHostArtifactManifest(SkillHostKind.OpenAi, path, Digest('2'), Digest('1')));
    }

    private static SkillManifestCandidate CreateManifest (
        IReadOnlyList<SkillName> dependencies,
        IReadOnlyList<SkillHostArtifactManifest> hostArtifacts)
    {
        return CreateManifest(
            SkillManifest.CurrentSchemaVersion,
            new SkillName("sample-skill"),
            "Sample Skill",
            "Use this sample skill for tests.",
            dependencies,
            hostArtifacts);
    }

    private static SkillManifestCandidate CreateManifest (
        int schemaVersion,
        SkillName skillName,
        string displayName,
        string description,
        IReadOnlyList<SkillName> dependencies,
        IReadOnlyList<SkillHostArtifactManifest> hostArtifacts)
    {
        return new SkillManifestCandidate(
            schemaVersion,
            new SkillBundleVersion(1),
            new SkillCatalogId("com.mackysoft.agent-skills"),
            new SkillCategory("core"),
            skillName,
            displayName,
            description,
            dependencies,
            Digest('0'),
            Digest('f'),
            hostArtifacts);
    }

    private static Sha256Digest Digest (char value)
    {
        return Sha256Digest.Parse(new string(value, 64));
    }
}

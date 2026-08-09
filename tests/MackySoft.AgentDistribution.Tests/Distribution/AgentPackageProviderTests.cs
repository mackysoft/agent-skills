using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Agents.Packaging;
using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentDistribution.Tests.Distribution;

public sealed class AgentPackageProviderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackageCatalogAsync_WhenSelectorsAreOmitted_SelectsAllAgentsAndDistinctResolvedSkills ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-package-provider", "all-agents");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var sameNameAgent = CreateAgent(
            skills,
            new AgentCategory("planning"),
            new AgentName(skills[0].Manifest.SkillName.Value),
            [skills[0].Manifest.SkillName]);
        var reviewer = CreateAgent(
            skills,
            new AgentCategory("quality"),
            new AgentName("reviewer"),
            [skills[0].Manifest.SkillName]);
        await WriteBundleAsync(scope.FullPath, skills, [reviewer, sameNameAgent]);
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackageCatalogAsync(CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(["planning", "quality"], result.Value!.SelectedCategories.Select(static category => category.Value).ToArray());
        Assert.Empty(result.Value.SelectedAgentNames);
        Assert.Equal(
            new[] { sameNameAgent.Manifest.AgentName.Value, reviewer.Manifest.AgentName.Value }.Order(StringComparer.Ordinal),
            result.Value.SelectedAgents.Select(static agent => agent.Manifest.AgentName.Value));
        Assert.Equal([skills[0].Manifest.SkillName.Value], result.Value.ResolvedSkills.Select(static skill => skill.Manifest.SkillName.Value));
        Assert.Equal(result.Value.BundleDescriptor.CatalogId, result.Value.SelectedAgents[0].Manifest.CatalogId);
        Assert.Equal(result.Value.BundleDescriptor.BundleVersion.Value, result.Value.ResolvedSkills[0].Manifest.SkillBundleVersion.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackageCatalogAsync_WhenNameMatchesSelectedCategory_ReturnsOnlyThatAgent ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-package-provider", "exact-selection");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var planner = CreateAgent(skills, new AgentCategory("planning"), new AgentName("planner"), [skills[0].Manifest.SkillName]);
        var reviewer = CreateAgent(skills, new AgentCategory("quality"), new AgentName("reviewer"), [skills[1].Manifest.SkillName]);
        await WriteBundleAsync(scope.FullPath, skills, [planner, reviewer]);
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackageCatalogAsync(["planning"], ["planner"], CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(["planner"], result.Value!.SelectedAgents.Select(static agent => agent.Manifest.AgentName.Value));
        Assert.Equal([skills[0].Manifest.SkillName.Value], result.Value.ResolvedSkills.Select(static skill => skill.Manifest.SkillName.Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackageCatalogAsync_RejectsUnknownCategory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-package-provider", "unknown-category");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        await WriteBundleAsync(scope.FullPath, skills, [CreateAgent(skills, new AgentCategory("planning"), new AgentName("planner"), [])]);
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackageCatalogAsync(["unknown"], [], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackageCatalogAsync_RejectsNameOutsideSelectedCategory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-package-provider", "category-name-mismatch");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var planner = CreateAgent(skills, new AgentCategory("planning"), new AgentName("planner"), []);
        var reviewer = CreateAgent(skills, new AgentCategory("quality"), new AgentName("reviewer"), []);
        await WriteBundleAsync(scope.FullPath, skills, [planner, reviewer]);
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackageCatalogAsync(["planning"], ["reviewer"], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetPackageCatalogAsync_RejectsUnknownAgentName ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-package-provider", "unknown-name");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        await WriteBundleAsync(scope.FullPath, skills, [CreateAgent(skills, new AgentCategory("planning"), new AgentName("planner"), [])]);
        var provider = CreateProvider(scope.FullPath);

        var result = await provider.GetPackageCatalogAsync([], ["unknown-agent"], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteBundleAsync_WhenAgentDependsOnMissingSkill_RejectsTheCanonicalBundle ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-agent-package-provider", "missing-agent-skill-dependency");
        var skills = await SkillTestData.GenerateFixturePackagesAsync();
        var agent = CreateAgent(
            skills,
            new AgentCategory("planning"),
            new AgentName("planner"),
            [new SkillName("missing-skill")]);

        var exception = await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(async () =>
            await WriteBundleAsync(scope.FullPath, skills, [agent]));

        Assert.Contains("missing skill dependencies", exception.Message, StringComparison.Ordinal);
    }

    private static AgentPackageProvider CreateProvider (string packageBaseDirectory)
    {
        var skillManifestSerializer = new SkillManifestJsonSerializer();
        var agentManifestSerializer = new AgentManifestJsonSerializer();
        var digestCalculator = new SkillDigestCalculator();
        var skillReader = new CanonicalSkillPackageReader(
            skillManifestSerializer,
            new SkillManifest.Factory(new SkillManifestDigestCalculator(skillManifestSerializer)),
            new CanonicalSkillPackage.Factory(digestCalculator, skillManifestSerializer));
        var agentManifestDigestCalculator = new AgentManifestDigestCalculator(agentManifestSerializer);
        var agentReader = new CanonicalAgentPackageReader(
            agentManifestSerializer,
            digestCalculator,
            agentManifestDigestCalculator);
        var bundleReader = new CanonicalAgentDistributionBundleReader(
            new AgentDistributionBundleJsonSerializer(),
            skillReader,
            agentReader,
            new AgentDistributionBundleDigestCalculator(skillManifestSerializer, agentManifestSerializer, digestCalculator));
        return new AgentPackageProvider(
            new BundledAgentDistributionPackageRootResolver(AbsolutePath.Parse(packageBaseDirectory)),
            bundleReader);
    }

    private static async Task WriteBundleAsync (
        string packageBaseDirectory,
        IReadOnlyList<CanonicalSkillPackage> skills,
        IReadOnlyList<CanonicalAgentPackage> agents)
    {
        var skillManifestSerializer = new SkillManifestJsonSerializer();
        var agentManifestSerializer = new AgentManifestJsonSerializer();
        var digestCalculator = new SkillDigestCalculator();
        var descriptor = new AgentDistributionBundleDescriptor(
            AgentDistributionBundleDefinition.CurrentSchemaVersion,
            skills[0].Manifest.CatalogId,
            new AgentDistributionBundleVersion(skills[0].Manifest.SkillBundleVersion.Value),
            new AgentDistributionBundleDigestCalculator(skillManifestSerializer, agentManifestSerializer, digestCalculator).ComputeDigest(skills, agents));
        var bundle = new CanonicalAgentDistributionBundle(descriptor, skills, agents);
        var agentManifestDigestCalculator = new AgentManifestDigestCalculator(agentManifestSerializer);
        var agentReader = new CanonicalAgentPackageReader(
            agentManifestSerializer,
            digestCalculator,
            agentManifestDigestCalculator);
        var bundleReader = new CanonicalAgentDistributionBundleReader(
            new AgentDistributionBundleJsonSerializer(),
            new CanonicalSkillPackageReader(
                skillManifestSerializer,
                new SkillManifest.Factory(new SkillManifestDigestCalculator(skillManifestSerializer)),
                new CanonicalSkillPackage.Factory(digestCalculator, skillManifestSerializer)),
            agentReader,
            new AgentDistributionBundleDigestCalculator(skillManifestSerializer, agentManifestSerializer, digestCalculator));
        var writer = new CanonicalAgentDistributionBundleWriter(
            new CanonicalSkillPackageWriter(),
            new CanonicalAgentPackageWriter(),
            new AgentDistributionBundleJsonSerializer(),
            bundleReader);

        var result = await writer.WriteAsync(bundle, AbsolutePath.Parse(Path.Combine(packageBaseDirectory, "skills")), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Failure?.Message);
    }

    private static CanonicalAgentPackage CreateAgent (
        IReadOnlyList<CanonicalSkillPackage> skills,
        AgentCategory category,
        AgentName agentName,
        IReadOnlyList<SkillName> skillDependencies)
    {
        var agentManifestSerializer = new AgentManifestJsonSerializer();
        var digestCalculator = new SkillDigestCalculator();
        var bundleVersion = new AgentDistributionBundleVersion(skills[0].Manifest.SkillBundleVersion.Value);
        const string instructions = "# Agent\n";
        var artifactPath = PackageRelativePath.Parse("hosts/codex/agent.toml");
        var instructionsPath = PackageRelativePath.Parse("AGENT.md");
        const string artifactContent = "name = \"fixture\"\n";
        var artifact = new AgentHostArtifactManifest(
            HostKind.Codex,
            artifactPath,
            digestCalculator.ComputeSingleFileDigest(artifactPath, artifactContent));
        var provisional = new AgentManifest(
            AgentManifest.CurrentSchemaVersion,
            bundleVersion,
            skills[0].Manifest.CatalogId,
            category,
            agentName,
            agentName.Value,
            $"Fixture {agentName.Value}.",
            skillDependencies,
            digestCalculator.ComputeSingleFileDigest(instructionsPath, instructions),
            Sha256Digest.Parse(new string('0', 64)),
            [artifact]);
        var manifest = new AgentManifest(
            provisional.SchemaVersion,
            provisional.BundleVersion,
            provisional.CatalogId,
            provisional.Category,
            provisional.AgentName,
            provisional.DisplayName,
            provisional.Description,
            provisional.SkillDependencies,
            provisional.ContentDigest,
            new AgentManifestDigestCalculator(agentManifestSerializer).ComputeManifestDigest(provisional),
            provisional.HostArtifacts);
        var files = new PackageTextFile[]
        {
            new(instructionsPath, instructions),
            new(artifactPath, artifactContent),
            new(PackageRelativePath.Parse("agent-manifest.json"), agentManifestSerializer.Serialize(manifest)),
        };

        return new CanonicalAgentPackage(manifest, files, agentManifestSerializer, digestCalculator);
    }
}

using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Agents.Packaging;
using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Distribution;

internal static class AgentDistributionTestData
{
    internal static AgentPackageCatalog CreateCatalog (
        IReadOnlyList<CanonicalSkillPackage> skills,
        IReadOnlyList<CanonicalAgentPackage> agents,
        IReadOnlyList<AgentName>? selectedAgentNames = null)
    {
        var descriptor = new AgentDistributionBundleDescriptor(
            AgentDistributionBundleDefinition.CurrentSchemaVersion,
            skills[0].Manifest.CatalogId,
            new AgentDistributionBundleVersion(skills[0].Manifest.SkillBundleVersion.Value),
            Sha256Digest.Parse(new string('f', 64)));

        return new AgentPackageCatalog(
            descriptor,
            selectedAgentNames ?? [],
            agents,
            skills);
    }

    internal static CanonicalAgentPackage CreateAgent (
        IReadOnlyList<CanonicalSkillPackage> skills,
        string agentName,
        HostKind hostId,
        string hostRelativeArtifactPath,
        IReadOnlyList<SkillName>? skillDependencies = null)
    {
        var manifestSerializer = new AgentManifestJsonSerializer();
        var digestCalculator = new PackageContentDigestCalculator();
        var bundleVersion = new AgentDistributionBundleVersion(skills[0].Manifest.SkillBundleVersion.Value);
        var canonicalAgentName = new AgentName(agentName);
        const string instructions = "# Agent\n";
        var artifactPath = AgentHostArtifactPackagePath.Create(hostId, PackageRelativePath.Parse(hostRelativeArtifactPath));
        var instructionsPath = PackageRelativePath.Parse("AGENT.md");
        var artifactContent = $"name = \"{agentName}\"\n";
        var artifact = new AgentHostArtifactManifest(
            hostId,
            artifactPath,
            digestCalculator.ComputeSingleFileDigest(artifactPath, artifactContent));
        var provisional = new AgentManifest(
            AgentManifest.CurrentSchemaVersion,
            bundleVersion,
            skills[0].Manifest.CatalogId,
            canonicalAgentName,
            agentName,
            $"Fixture {agentName}.",
            skillDependencies ?? skills.Select(static skill => skill.Manifest.SkillName).ToArray(),
            digestCalculator.ComputeSingleFileDigest(instructionsPath, instructions),
            Sha256Digest.Parse(new string('0', 64)),
            [artifact]);
        var manifest = new AgentManifest(
            provisional.SchemaVersion,
            provisional.BundleVersion,
            provisional.CatalogId,
            provisional.AgentName,
            provisional.DisplayName,
            provisional.Description,
            provisional.SkillDependencies,
            provisional.ContentDigest,
            new AgentManifestDigestCalculator(manifestSerializer).ComputeManifestDigest(provisional),
            provisional.HostArtifacts);

        return new CanonicalAgentPackage(
            manifest,
            [
                new PackageTextFile(instructionsPath, instructions),
                new PackageTextFile(artifactPath, artifactContent),
                new PackageTextFile(PackageRelativePath.Parse("agent-manifest.json"), manifestSerializer.Serialize(manifest)),
            ],
            manifestSerializer,
            digestCalculator);
    }
}

using MackySoft.AgentSkills.Agents.Manifests;
using MackySoft.AgentSkills.Agents.Packaging;
using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Distribution;

internal static class AgentDistributionTestData
{
    internal static AgentPackageCatalog CreateCatalog (
        IReadOnlyList<CanonicalSkillPackage> skills,
        IReadOnlyList<CanonicalAgentPackage> agents,
        IReadOnlyList<AgentCategory>? selectedCategories = null,
        IReadOnlyList<AgentName>? selectedAgentNames = null)
    {
        var descriptor = new AgentSkillsBundleDescriptor(
            AgentSkillsBundleDefinition.CurrentSchemaVersion,
            skills[0].Manifest.CatalogId,
            new AgentSkillsBundleVersion(skills[0].Manifest.SkillBundleVersion.Value),
            Sha256Digest.Parse(new string('f', 64)));

        return new AgentPackageCatalog(
            descriptor,
            selectedCategories ?? agents.Select(static agent => agent.Manifest.Category).Distinct().ToArray(),
            selectedAgentNames ?? [],
            agents,
            skills);
    }

    internal static CanonicalAgentPackage CreateAgent (
        IReadOnlyList<CanonicalSkillPackage> skills,
        string category,
        string agentName,
        HostKind hostId,
        string hostRelativeArtifactPath,
        IReadOnlyList<SkillName>? skillDependencies = null)
    {
        var manifestSerializer = new AgentManifestJsonSerializer();
        var digestCalculator = new SkillDigestCalculator();
        var bundleVersion = new AgentSkillsBundleVersion(skills[0].Manifest.SkillBundleVersion.Value);
        var canonicalAgentName = new AgentName(agentName);
        const string instructions = "# Agent\n";
        var artifactPath = PackageRelativePath.Parse($"hosts/{Vocabulary.GetText(hostId)}/{hostRelativeArtifactPath}");
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
            new AgentCategory(category),
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
            provisional.Category,
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

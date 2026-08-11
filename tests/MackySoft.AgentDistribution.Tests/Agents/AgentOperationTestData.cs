using MackySoft.AgentDistribution.Agents.Doctor;
using MackySoft.AgentDistribution.Agents.Installation.Services;
using MackySoft.AgentDistribution.Agents.Installation.State;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Agents.Packaging;
using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Distribution;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Agents;

internal static class AgentOperationTestData
{
    public static CanonicalAgentPackage CreateAgent (
        IReadOnlyList<CanonicalSkillPackage> skills,
        string agentName,
        string artifactRelativePath,
        string artifactContent,
        IReadOnlyList<SkillName>? skillDependencies = null)
    {
        var serializer = new AgentManifestJsonSerializer();
        var digestCalculator = new PackageContentDigestCalculator();
        var packageArtifactPath = AgentHostArtifactPackagePath.Create(HostKind.Codex, PackageRelativePath.Parse(artifactRelativePath));
        var instructionsPath = PackageRelativePath.Parse("AGENT.md");
        var artifact = new AgentHostArtifactManifest(
            HostKind.Codex,
            packageArtifactPath,
            digestCalculator.ComputeSingleFileDigest(packageArtifactPath, artifactContent));
        var provisional = new AgentManifest(
            AgentManifest.CurrentSchemaVersion,
            new AgentDistributionBundleVersion(skills[0].Manifest.SkillBundleVersion.Value),
            skills[0].Manifest.CatalogId,
            new AgentName(agentName),
            agentName,
            $"Fixture {agentName}.",
            skillDependencies ?? [],
            digestCalculator.ComputeSingleFileDigest(instructionsPath, "# Fixture agent\n"),
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
            new AgentManifestDigestCalculator(serializer).ComputeManifestDigest(provisional),
            provisional.HostArtifacts);
        return new CanonicalAgentPackage(
            manifest,
            [
                new PackageTextFile(instructionsPath, "# Fixture agent\n"),
                new PackageTextFile(packageArtifactPath, artifactContent),
                new PackageTextFile(PackageRelativePath.Parse("agent-manifest.json"), serializer.Serialize(manifest)),
            ],
            serializer,
            digestCalculator);
    }

    public static AgentPackageCatalog CreateCatalog (
        IReadOnlyList<CanonicalSkillPackage> skills,
        IReadOnlyList<CanonicalAgentPackage> agents,
        IReadOnlyList<CanonicalSkillPackage>? resolvedSkills = null,
        IReadOnlyList<AgentName>? selectedAgentNames = null)
    {
        var descriptor = new AgentDistributionBundleDescriptor(
            AgentDistributionBundleDefinition.CurrentSchemaVersion,
            skills[0].Manifest.CatalogId,
            new AgentDistributionBundleVersion(skills[0].Manifest.SkillBundleVersion.Value),
            Sha256Digest.Parse(new string('1', 64)));
        return new AgentPackageCatalog(
            descriptor,
            selectedAgentNames ?? [],
            agents,
            resolvedSkills ?? []);
    }

    public static AgentInstallTargetResolver CreateAgentTargetResolver (string homeDirectory)
    {
        return new AgentInstallTargetResolver(
            new AgentUserTargetRootResolver(() => homeDirectory, _ => null));
    }

    public static AgentInstalledTargetInspector CreateInspector (
        AgentInstallationStatePathResolver statePathResolver,
        AgentInstallationStateStore stateStore,
        PackageContentDigestCalculator digestCalculator)
    {
        return new AgentInstalledTargetInspector(statePathResolver, stateStore, digestCalculator);
    }

    public static AgentInstallService CreateInstallService (string homeDirectory)
    {
        var statePathResolver = new AgentInstallationStatePathResolver();
        var stateStore = new AgentInstallationStateStore(new AgentInstallationStateJsonSerializer());
        var digestCalculator = new PackageContentDigestCalculator();
        return new AgentInstallService(
            CreateAgentTargetResolver(homeDirectory),
            CreateInspector(statePathResolver, stateStore, digestCalculator),
            digestCalculator,
            statePathResolver,
            stateStore,
            SkillTestData.CreateInstallService());
    }

    public static AgentUpdateService CreateUpdateService (string homeDirectory)
    {
        var statePathResolver = new AgentInstallationStatePathResolver();
        var stateStore = new AgentInstallationStateStore(new AgentInstallationStateJsonSerializer());
        var digestCalculator = new PackageContentDigestCalculator();
        return new AgentUpdateService(
            CreateAgentTargetResolver(homeDirectory),
            CreateInspector(statePathResolver, stateStore, digestCalculator),
            digestCalculator,
            statePathResolver,
            stateStore,
            SkillTestData.CreateUpdateService());
    }

    public static AgentUninstallService CreateUninstallService (string homeDirectory)
    {
        var statePathResolver = new AgentInstallationStatePathResolver();
        var stateStore = new AgentInstallationStateStore(new AgentInstallationStateJsonSerializer());
        return new AgentUninstallService(
            CreateAgentTargetResolver(homeDirectory),
            CreateInspector(statePathResolver, stateStore, new PackageContentDigestCalculator()),
            statePathResolver,
            stateStore);
    }

    public static AgentPruneService CreatePruneService (string homeDirectory)
    {
        var statePathResolver = new AgentInstallationStatePathResolver();
        var stateStore = new AgentInstallationStateStore(new AgentInstallationStateJsonSerializer());
        return new AgentPruneService(
            CreateAgentTargetResolver(homeDirectory),
            CreateInspector(statePathResolver, stateStore, new PackageContentDigestCalculator()),
            statePathResolver,
            stateStore);
    }

    public static AgentDoctorService CreateDoctorService (string homeDirectory)
    {
        var statePathResolver = new AgentInstallationStatePathResolver();
        var stateStore = new AgentInstallationStateStore(new AgentInstallationStateJsonSerializer());
        return new AgentDoctorService(
            CreateAgentTargetResolver(homeDirectory),
            SkillTestData.CreateInstallTargetResolver(),
            CreateInspector(statePathResolver, stateStore, new PackageContentDigestCalculator()),
            SkillTestData.CreateDoctorService());
    }
}

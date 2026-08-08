using MackySoft.AgentSkills.Agents;
using MackySoft.AgentSkills.Agents.Doctor;
using MackySoft.AgentSkills.Agents.Installation.Services;
using MackySoft.AgentSkills.Agents.Installation.State;
using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Agents.Manifests;
using MackySoft.AgentSkills.Agents.Packaging;
using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Agents;

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
        var digestCalculator = new SkillDigestCalculator();
        var packageArtifactPath = $"hosts/openai/{artifactRelativePath}";
        var artifact = new AgentHostArtifactManifest(
            AgentHostKind.OpenAi,
            packageArtifactPath,
            digestCalculator.ComputeSingleFileDigest(packageArtifactPath, artifactContent));
        var provisional = new AgentManifest(
            AgentManifest.CurrentSchemaVersion,
            new AgentSkillsBundleVersion(skills[0].Manifest.SkillBundleVersion.Value),
            skills[0].Manifest.CatalogId,
            new AgentCategory("engineering"),
            new AgentName(agentName),
            agentName,
            $"Fixture {agentName}.",
            skillDependencies ?? [],
            digestCalculator.ComputeSingleFileDigest("AGENT.md", "# Fixture agent\n"),
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
            new AgentManifestDigestCalculator(serializer).ComputeManifestDigest(provisional),
            provisional.HostArtifacts);
        return new CanonicalAgentPackage(
            manifest,
            [
                new SkillPackageFile("AGENT.md", "# Fixture agent\n"),
                new SkillPackageFile(packageArtifactPath, artifactContent),
                new SkillPackageFile("agent-manifest.json", serializer.Serialize(manifest)),
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
        var descriptor = new AgentSkillsBundleDescriptor(
            AgentSkillsBundleDefinition.CurrentSchemaVersion,
            skills[0].Manifest.CatalogId,
            new AgentSkillsBundleVersion(skills[0].Manifest.SkillBundleVersion.Value),
            Sha256Digest.Parse(new string('1', 64)));
        return new AgentPackageCatalog(
            descriptor,
            agents.Select(static agent => agent.Manifest.Category).Distinct().ToArray(),
            selectedAgentNames ?? [],
            agents,
            resolvedSkills ?? []);
    }

    public static AgentInstallTargetResolver CreateAgentTargetResolver (string homeDirectory)
    {
        return new AgentInstallTargetResolver(
            new AgentHostAdapterSet(),
            new AgentUserTargetRootResolver(() => homeDirectory, _ => null));
    }

    public static AgentInstalledTargetInspector CreateInspector (
        AgentInstallationStatePathResolver statePathResolver,
        AgentInstallationStateStore stateStore,
        SkillDigestCalculator digestCalculator)
    {
        return new AgentInstalledTargetInspector(statePathResolver, stateStore, digestCalculator);
    }

    public static AgentInstallService CreateInstallService (string homeDirectory)
    {
        var statePathResolver = new AgentInstallationStatePathResolver();
        var stateStore = new AgentInstallationStateStore(new AgentInstallationStateJsonSerializer());
        var digestCalculator = new SkillDigestCalculator();
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
        var digestCalculator = new SkillDigestCalculator();
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
            CreateInspector(statePathResolver, stateStore, new SkillDigestCalculator()),
            statePathResolver,
            stateStore);
    }

    public static AgentPruneService CreatePruneService (string homeDirectory)
    {
        var statePathResolver = new AgentInstallationStatePathResolver();
        var stateStore = new AgentInstallationStateStore(new AgentInstallationStateJsonSerializer());
        return new AgentPruneService(
            CreateAgentTargetResolver(homeDirectory),
            CreateInspector(statePathResolver, stateStore, new SkillDigestCalculator()),
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
            CreateInspector(statePathResolver, stateStore, new SkillDigestCalculator()),
            SkillTestData.CreateDoctorService());
    }
}

using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Agents.Packaging;
using MackySoft.AgentDistribution.Agents.Sources;
using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Agents.Generation;

/// <summary> Generates canonical agent packages from validated host-independent source definitions. </summary>
internal sealed class AgentPackageGenerationService
{
    private readonly AgentManifestJsonSerializer manifestSerializer;
    private readonly AgentManifestDigestCalculator manifestDigestCalculator;
    private readonly PackageContentDigestCalculator digestCalculator;

    /// <summary> Initializes the generator. </summary>
    public AgentPackageGenerationService (AgentManifestJsonSerializer manifestSerializer, AgentManifestDigestCalculator manifestDigestCalculator, PackageContentDigestCalculator digestCalculator)
    {
        this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
        this.manifestDigestCalculator = manifestDigestCalculator ?? throw new ArgumentNullException(nameof(manifestDigestCalculator));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Generates one canonical agent package. </summary>
    public CanonicalAgentPackage Generate (AgentDistributionBundleDefinition bundle, AgentSourceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(definition);
        var instructions = AgentDistributionTextNormalizer.NormalizeToLf(definition.InstructionsTemplate);
        var instructionsPath = PackageRelativePath.Parse("AGENT.md");
        var files = new List<PackageTextFile> { new(instructionsPath, instructions) };
        var artifacts = new List<AgentHostArtifactManifest>();
        foreach (var binding in definition.HostBindings)
        {
            var adapter = HostRegistration.Get(binding.HostId).Value!.AgentArtifactAdapter;
            foreach (var file in adapter.BuildArtifacts(definition.Metadata, instructions, binding.Json).Files)
            {
                var path = AgentHostArtifactPackagePath.Create(binding.HostId, file.RelativePath);
                files.Add(new PackageTextFile(path, file.Content));
                artifacts.Add(new AgentHostArtifactManifest(binding.HostId, path, digestCalculator.ComputeSingleFileDigest(path, file.Content)));
            }
        }

        var contentDigest = digestCalculator.ComputeSingleFileDigest(instructionsPath, instructions);
        var placeholder = Sha256Digest.Parse(new string('0', 64));
        var provisional = new AgentManifest(AgentManifest.CurrentSchemaVersion, bundle.BundleVersion, bundle.CatalogId, definition.Metadata.AgentName, definition.Metadata.DisplayName, definition.Metadata.Description, definition.Metadata.SkillDependencies, contentDigest, placeholder, artifacts);
        var manifest = new AgentManifest(provisional.SchemaVersion, provisional.BundleVersion, provisional.CatalogId, provisional.AgentName, provisional.DisplayName, provisional.Description, provisional.SkillDependencies, provisional.ContentDigest, manifestDigestCalculator.ComputeManifestDigest(provisional), provisional.HostArtifacts);
        files.Add(new PackageTextFile(PackageRelativePath.Parse("agent-manifest.json"), manifestSerializer.Serialize(manifest)));
        return new CanonicalAgentPackage(manifest, files, manifestSerializer, digestCalculator);
    }
}

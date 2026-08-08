using MackySoft.AgentSkills.Agents.Manifests;
using MackySoft.AgentSkills.Agents.Packaging;
using MackySoft.AgentSkills.Agents.Sources;
using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Generation;

/// <summary> Generates canonical agent packages from validated host-independent source definitions. </summary>
internal sealed class AgentPackageGenerationService
{
    private readonly AgentHostAdapterSet hostAdapters;
    private readonly AgentManifestJsonSerializer manifestSerializer;
    private readonly AgentManifestDigestCalculator manifestDigestCalculator;
    private readonly SkillDigestCalculator digestCalculator;

    /// <summary> Initializes the generator. </summary>
    public AgentPackageGenerationService (AgentHostAdapterSet hostAdapters, AgentManifestJsonSerializer manifestSerializer, AgentManifestDigestCalculator manifestDigestCalculator, SkillDigestCalculator digestCalculator)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
        this.manifestDigestCalculator = manifestDigestCalculator ?? throw new ArgumentNullException(nameof(manifestDigestCalculator));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Generates one canonical agent package. </summary>
    public CanonicalAgentPackage Generate (AgentSkillsBundleDefinition bundle, AgentSourceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(definition);
        var instructions = SkillTextNormalizer.NormalizeToLf(definition.InstructionsTemplate);
        var files = new List<SkillPackageFile> { new("AGENT.md", instructions) };
        var artifacts = new List<AgentHostArtifactManifest>();
        foreach (var binding in definition.HostBindings)
        {
            var adapter = hostAdapters.GetAdapter(binding.HostId).Value!;
            foreach (var file in adapter.BuildArtifacts(definition.Metadata, instructions, binding.Json).Files)
            {
                var path = $"hosts/{Vocabulary.GetText(binding.HostId)}/{file.RelativePath}";
                files.Add(new SkillPackageFile(path, file.Content));
                artifacts.Add(new AgentHostArtifactManifest(binding.HostId, path, digestCalculator.ComputeSingleFileDigest(path, file.Content)));
            }
        }

        var contentDigest = digestCalculator.ComputeSingleFileDigest("AGENT.md", instructions);
        var placeholder = Sha256Digest.Parse(new string('0', 64));
        var provisional = new AgentManifest(AgentManifest.CurrentSchemaVersion, bundle.BundleVersion, bundle.CatalogId, definition.Metadata.Category, definition.Metadata.AgentName, definition.Metadata.DisplayName, definition.Metadata.Description, definition.Metadata.SkillDependencies, contentDigest, placeholder, artifacts);
        var manifest = new AgentManifest(provisional.SchemaVersion, provisional.BundleVersion, provisional.CatalogId, provisional.Category, provisional.AgentName, provisional.DisplayName, provisional.Description, provisional.SkillDependencies, provisional.ContentDigest, manifestDigestCalculator.ComputeManifestDigest(provisional), provisional.HostArtifacts);
        files.Add(new SkillPackageFile("agent-manifest.json", manifestSerializer.Serialize(manifest)));
        return new CanonicalAgentPackage(manifest, files, manifestSerializer, digestCalculator);
    }
}

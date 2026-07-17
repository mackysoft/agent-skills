using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Manifests;

/// <summary> Represents the canonical <c>agent-skill.json</c> manifest. </summary>
public sealed class SkillManifest
{
    /// <summary> Initializes one immutable canonical manifest. </summary>
    /// <param name="candidate"> The field-valid candidate whose host shape has been validated. </param>
    /// <param name="manifestDigest"> The canonical manifest digest excluding this field. </param>
    private SkillManifest (
        SkillManifestCandidate candidate,
        Sha256Digest manifestDigest)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        SchemaVersion = candidate.SchemaVersion;
        SkillBundleVersion = candidate.SkillBundleVersion;
        CatalogId = candidate.CatalogId;
        Category = candidate.Category;
        SkillName = candidate.SkillName;
        DisplayName = candidate.DisplayName;
        Description = candidate.Description;
        Dependencies = candidate.Dependencies;
        ContentDigest = candidate.ContentDigest;
        ManifestDigest = manifestDigest ?? throw new ArgumentNullException(nameof(manifestDigest));
        HostArtifacts = candidate.HostArtifacts;
    }

    /// <summary> Gets the manifest schema version. </summary>
    public int SchemaVersion { get; }

    /// <summary> Gets the product-owned SKILL bundle version. </summary>
    public int SkillBundleVersion { get; }

    /// <summary> Gets the stable SKILL catalog ID. </summary>
    public SkillCatalogId CatalogId { get; }

    /// <summary> Gets the product-owned SKILL category. </summary>
    public SkillCategory Category { get; }

    /// <summary> Gets the skill name. </summary>
    public SkillName SkillName { get; }

    /// <summary> Gets the display name shown by SKILL listing commands. </summary>
    public string DisplayName { get; }

    /// <summary> Gets the host-independent SKILL description. </summary>
    public string Description { get; }

    /// <summary> Gets an immutable snapshot of dependent skill names. </summary>
    public IReadOnlyList<SkillName> Dependencies { get; }

    /// <summary> Gets the host-independent content digest. </summary>
    public Sha256Digest ContentDigest { get; }

    /// <summary> Gets the canonical manifest digest excluding this field. </summary>
    public Sha256Digest ManifestDigest { get; }

    /// <summary> Gets an immutable snapshot of host-specific artifact digests. </summary>
    public IReadOnlyList<SkillHostArtifactManifest> HostArtifacts { get; }

    /// <summary> Gets the current manifest schema version. </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary> Validates manifest candidates and creates canonical manifest instances. </summary>
    public sealed class Factory
    {
        private readonly SkillHostAdapterSet hostAdapters;
        private readonly SkillManifestDigestCalculator manifestDigestCalculator;

        /// <summary> Initializes the canonical manifest construction boundary. </summary>
        /// <param name="hostAdapters"> The complete supported host adapter set. </param>
        /// <param name="manifestDigestCalculator"> The canonical manifest digest calculator. </param>
        public Factory (
            SkillHostAdapterSet hostAdapters,
            SkillManifestDigestCalculator manifestDigestCalculator)
        {
            this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
            this.manifestDigestCalculator = manifestDigestCalculator ?? throw new ArgumentNullException(nameof(manifestDigestCalculator));
        }

        /// <summary> Validates one parsed or generated candidate and creates its canonical manifest. </summary>
        internal SkillOperationResult<SkillManifest> CreateCanonical (SkillManifestCandidate candidate)
        {
            var shapeResult = ValidateShape(candidate);
            if (!shapeResult.IsSuccess)
            {
                return ManifestFailure(shapeResult.Failure!.Message);
            }

            var expectedManifestDigest = manifestDigestCalculator.ComputeManifestDigest(candidate);
            if (candidate.ManifestDigest is not null && expectedManifestDigest != candidate.ManifestDigest)
            {
                return ManifestFailure("agent-skill.json manifestDigest does not match manifest content.");
            }

            return SkillOperationResult<SkillManifest>.Success(new SkillManifest(candidate, expectedManifestDigest));
        }

        /// <summary>
        /// Validates an installed manifest candidate's shape and normalizes its digest while its source text retains drift evidence.
        /// </summary>
        internal SkillOperationResult<SkillManifest> CreateCanonicalFromInstalledShape (SkillManifestCandidate candidate)
        {
            var shapeResult = ValidateShape(candidate);
            if (!shapeResult.IsSuccess)
            {
                return ManifestFailure(shapeResult.Failure!.Message);
            }

            var manifestDigest = manifestDigestCalculator.ComputeManifestDigest(candidate);
            return SkillOperationResult<SkillManifest>.Success(new SkillManifest(candidate, manifestDigest));
        }

        private SkillOperationResult<bool> ValidateShape (SkillManifestCandidate candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            var expectedHosts = hostAdapters.Adapters.Select(static adapter => adapter.Descriptor.Host).Order().ToArray();
            var artifactHosts = candidate.HostArtifacts.Select(static artifact => artifact.Host).Order().ToArray();
            if (!expectedHosts.SequenceEqual(artifactHosts))
            {
                return ShapeFailure("agent-skill.json hostArtifacts must contain exactly all supported hosts.");
            }

            var artifactByHost = candidate.HostArtifacts.ToDictionary(static artifact => artifact.Host);
            foreach (var adapter in hostAdapters.Adapters)
            {
                var artifact = artifactByHost[adapter.Descriptor.Host];
                var metadataArtifactPath = adapter.Descriptor.MetadataArtifactPath;
                if (metadataArtifactPath is null)
                {
                    if (artifact.Path is not null || artifact.Digest is not null)
                    {
                        return ShapeFailure($"Host artifact '{ContractLiteralCodec.ToValue(artifact.Host)}' must not contain file artifact fields.");
                    }

                    continue;
                }

                if (!string.Equals(artifact.Path, metadataArtifactPath, StringComparison.Ordinal) || artifact.Digest is null)
                {
                    return ShapeFailure($"Host artifact '{ContractLiteralCodec.ToValue(artifact.Host)}' must contain metadata artifact digest.");
                }
            }

            return SkillOperationResult<bool>.Success(true);
        }

        private static SkillOperationResult<SkillManifest> ManifestFailure (string message)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
        }

        private static SkillOperationResult<bool> ShapeFailure (string message)
        {
            return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
        }
    }
}

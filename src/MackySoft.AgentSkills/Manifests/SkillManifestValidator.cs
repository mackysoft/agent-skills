using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Manifests;

/// <summary> Validates canonical SKILL manifests. </summary>
public sealed class SkillManifestValidator
{
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillManifestDigestCalculator manifestDigestCalculator;

    /// <summary> Initializes a new instance of the <see cref="SkillManifestValidator" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="manifestDigestCalculator"> The canonical manifest digest calculator. </param>
    public SkillManifestValidator (
        SkillHostAdapterSet hostAdapters,
        SkillManifestDigestCalculator manifestDigestCalculator)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.manifestDigestCalculator = manifestDigestCalculator ?? throw new ArgumentNullException(nameof(manifestDigestCalculator));
    }

    /// <summary> Validates one manifest. </summary>
    /// <param name="manifest"> The manifest. </param>
    /// <returns> The valid manifest or validation failure. </returns>
    public SkillOperationResult<SkillManifest> Validate (SkillManifest manifest)
    {
        var shapeResult = ValidateShape(manifest);
        if (!shapeResult.IsSuccess)
        {
            return shapeResult;
        }

        var expectedManifestDigest = manifestDigestCalculator.ComputeManifestDigest(manifest);
        if (!string.Equals(expectedManifestDigest, manifest.ManifestDigest, StringComparison.Ordinal))
        {
            return Failure("agent-skill.json manifestDigest does not match manifest content.");
        }

        return SkillOperationResult<SkillManifest>.Success(manifest);
    }

    /// <summary> Validates one manifest shape without checking whether <c>manifestDigest</c> matches the manifest content. </summary>
    /// <param name="manifest"> The manifest. </param>
    /// <returns> The valid manifest shape or validation failure. </returns>
    internal SkillOperationResult<SkillManifest> ValidateShape (SkillManifest manifest)
    {
        return ValidateShape(manifest, allowLegacySkillBundleVersion: false);
    }

    /// <summary> Validates one installed manifest shape while allowing packages created before <c>skillBundleVersion</c> existed. </summary>
    /// <param name="manifest"> The installed manifest. </param>
    /// <returns> The valid installed manifest shape or validation failure. </returns>
    internal SkillOperationResult<SkillManifest> ValidateInstalledShape (SkillManifest manifest)
    {
        return ValidateShape(manifest, allowLegacySkillBundleVersion: true);
    }

    private SkillOperationResult<SkillManifest> ValidateShape (
        SkillManifest manifest,
        bool allowLegacySkillBundleVersion)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.SchemaVersion != SkillManifest.CurrentSchemaVersion)
        {
            return Failure($"Unsupported agent-skill.json schemaVersion: {manifest.SchemaVersion}");
        }

        if (manifest.SkillBundleVersion <= 0 && (!allowLegacySkillBundleVersion || manifest.SkillBundleVersion != 0))
        {
            return Failure("agent-skill.json skillBundleVersion must be a positive integer.");
        }

        if (!manifest.SkillName.IsInitialized)
        {
            return Failure("agent-skill.json skillName must be valid.");
        }

        if (string.IsNullOrWhiteSpace(manifest.DisplayName)
            || string.IsNullOrWhiteSpace(manifest.Description)
            || manifest.Description.Length > 1024)
        {
            return Failure("agent-skill.json displayName and description must be valid.");
        }

        if (manifest.Tier is null)
        {
            return Failure("agent-skill.json tier must be valid.");
        }

        if (manifest.CatalogId is null)
        {
            return Failure("agent-skill.json catalogId must be valid.");
        }

        if (!ValidateDependencies(manifest))
        {
            return Failure("agent-skill.json dependencies must be valid safe SKILL identifiers and must not contain duplicates or self-dependencies.");
        }

        if (!Sha256LowerHex.IsDigestText(manifest.ContentDigest))
        {
            return Failure("agent-skill.json contentDigest must be a lowercase SHA-256 hex digest.");
        }

        if (!Sha256LowerHex.IsDigestText(manifest.ManifestDigest))
        {
            return Failure("agent-skill.json manifestDigest must be a lowercase SHA-256 hex digest.");
        }

        var expectedHosts = hostAdapters.Adapters.Select(static adapter => adapter.Descriptor.HostKey).Order(StringComparer.Ordinal).ToArray();
        var artifactHosts = manifest.HostArtifacts.Select(static artifact => artifact.Host).Order(StringComparer.Ordinal).ToArray();
        if (!expectedHosts.SequenceEqual(artifactHosts))
        {
            return Failure("agent-skill.json hostArtifacts must contain exactly all supported hosts.");
        }

        var artifactByHost = manifest.HostArtifacts.ToDictionary(static artifact => artifact.Host, StringComparer.Ordinal);
        foreach (var adapter in hostAdapters.Adapters)
        {
            var artifact = artifactByHost[adapter.Descriptor.HostKey];
            var metadataArtifactPath = adapter.Descriptor.MetadataArtifactPath;
            if (!Sha256LowerHex.IsDigestText(artifact.MaterializedFrontmatterDigest))
            {
                return Failure($"Host artifact '{artifact.Host}' frontmatter digest must be a lowercase SHA-256 hex digest.");
            }

            if (metadataArtifactPath is null)
            {
                if (artifact.Path is not null || artifact.Digest is not null)
                {
                    return Failure($"Host artifact '{artifact.Host}' must not contain file artifact fields.");
                }

                continue;
            }

            if (!string.Equals(artifact.Path, metadataArtifactPath, StringComparison.Ordinal) || !Sha256LowerHex.IsDigestText(artifact.Digest))
            {
                return Failure($"Host artifact '{artifact.Host}' must contain metadata artifact digest.");
            }
        }

        return SkillOperationResult<SkillManifest>.Success(manifest);
    }

    private static bool ValidateDependencies (SkillManifest manifest)
    {
        if (manifest.Dependencies is null)
        {
            return false;
        }

        var dependencySet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dependency in manifest.Dependencies)
        {
            if (!dependency.IsInitialized
                || string.Equals(manifest.SkillName.Value, dependency.Value, StringComparison.Ordinal)
                || !dependencySet.Add(dependency.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static SkillOperationResult<SkillManifest> Failure (string message)
    {
        return SkillOperationResult<SkillManifest>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
    }
}

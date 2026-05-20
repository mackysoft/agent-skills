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
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.SchemaVersion != SkillManifest.CurrentSchemaVersion)
        {
            return Failure($"Unsupported agent-skill.json schemaVersion: {manifest.SchemaVersion}");
        }

        if (!IsSafeSkillName(manifest.SkillName))
        {
            return Failure("agent-skill.json skillName must be a safe SKILL identifier.");
        }

        if (string.IsNullOrWhiteSpace(manifest.DisplayName)
            || string.IsNullOrWhiteSpace(manifest.Description)
            || manifest.Description.Length > 1024)
        {
            return Failure("agent-skill.json displayName and description must be valid.");
        }

        if (!IsSha256Digest(manifest.ContentDigest))
        {
            return Failure("agent-skill.json contentDigest must be a sha256 digest.");
        }

        if (!IsSha256Digest(manifest.ManifestDigest))
        {
            return Failure("agent-skill.json manifestDigest must be a sha256 digest.");
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
            if (!IsSha256Digest(artifact.MaterializedFrontmatterDigest))
            {
                return Failure($"Host artifact '{artifact.Host}' frontmatter digest must be a sha256 digest.");
            }

            if (adapter.MetadataArtifactPath is null)
            {
                if (artifact.Path is not null || artifact.Digest is not null)
                {
                    return Failure($"Host artifact '{artifact.Host}' must not contain file artifact fields.");
                }

                continue;
            }

            if (!string.Equals(artifact.Path, adapter.MetadataArtifactPath, StringComparison.Ordinal) || !IsSha256Digest(artifact.Digest))
            {
                return Failure($"Host artifact '{artifact.Host}' must contain metadata artifact digest.");
            }
        }

        return SkillOperationResult<SkillManifest>.Success(manifest);
    }

    private static SkillOperationResult<SkillManifest> Failure (string message)
    {
        return SkillOperationResult<SkillManifest>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
    }

    private static bool IsSha256Digest (string? value)
    {
        if (value is null || !value.StartsWith("sha256:", StringComparison.Ordinal) || value.Length != 71)
        {
            return false;
        }

        return value.AsSpan("sha256:".Length).IndexOfAnyExcept("0123456789abcdef") < 0;
    }

    private static bool IsSafeSkillName (string? skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName) || !IsAsciiLowercaseLetterOrDigit(skillName[0]))
        {
            return false;
        }

        for (var i = 1; i < skillName.Length; i++)
        {
            var character = skillName[i];
            if (character != '-' && !IsAsciiLowercaseLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLowercaseLetterOrDigit (char character)
    {
        return character is (>= 'a' and <= 'z') or (>= '0' and <= '9');
    }
}

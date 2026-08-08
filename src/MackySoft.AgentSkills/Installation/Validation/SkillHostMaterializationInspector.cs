using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Paths;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Installation.Validation;

/// <summary> Inspects installed files to determine whether they belong to the requested host. </summary>
public sealed class SkillHostMaterializationInspector
{
    private readonly SkillDigestCalculator digestCalculator;

    /// <summary> Initializes a new instance of the <see cref="SkillHostMaterializationInspector" /> class. </summary>
    /// <param name="digestCalculator"> The digest calculator. </param>
    public SkillHostMaterializationInspector (SkillDigestCalculator digestCalculator)
    {
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Determines whether a skill directory is materialized for the requested host. </summary>
    /// <param name="skillDirectory"> The skill directory. </param>
    /// <param name="manifest"> The canonical manifest. </param>
    /// <param name="host"> The requested host. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> <see langword="true" /> when files match the requested host; otherwise <see langword="false" />. </returns>
    public async ValueTask<SkillOperationResult<bool>> MatchesHostAsync (
        AbsolutePath skillDirectory,
        SkillManifest manifest,
        HostKind host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillDirectory);
        ArgumentNullException.ThrowIfNull(manifest);
        cancellationToken.ThrowIfCancellationRequested();

        var registrationResult = HostRegistration.Get(host);
        if (!registrationResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(
                registrationResult.Failure!.Code,
                registrationResult.Failure.Message);
        }

        var registration = registrationResult.Value!;
        var registeredHost = registration.Host;
        var descriptorMetadataArtifactPath = registration.Skill.MetadataArtifactPath;
        var expectedArtifact = manifest.HostArtifacts.SingleOrDefault(artifact => artifact.Host == registeredHost);
        if (expectedArtifact is null)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"Manifest does not contain host artifact '{Vocabulary.GetText(registeredHost)}'.");
        }

        var skillPathResult = PackagePathResolver.ResolveRegularFile(skillDirectory, PackageRelativePath.Parse("SKILL.md"));
        if (!skillPathResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(skillPathResult.Failure!.Code, skillPathResult.Failure.Message);
        }

        var skillPath = skillPathResult.Value!;
        if (!File.Exists(skillPath.Value))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        var skillText = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(skillPath.Value, cancellationToken).ConfigureAwait(false));
        if (!TryExtractFrontmatter(skillText, out var frontmatter))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        var actualFrontmatterDigest = digestCalculator.ComputeSingleFileDigest(PackageRelativePath.Parse("SKILL.md.frontmatter"), frontmatter);
        if (actualFrontmatterDigest != expectedArtifact.MaterializedFrontmatterDigest)
        {
            return SkillOperationResult<bool>.Success(false);
        }

        if (descriptorMetadataArtifactPath is null)
        {
            return expectedArtifact.Path is null && expectedArtifact.Digest is null
                ? SkillOperationResult<bool>.Success(true)
                : SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.ManifestInvalid,
                    $"Manifest host artifact '{Vocabulary.GetText(registeredHost)}' must not contain metadata artifact fields.");
        }

        var metadataArtifactPath = expectedArtifact.Path;
        var metadataArtifactDigest = expectedArtifact.Digest;
        if (metadataArtifactPath is null
            || metadataArtifactPath != descriptorMetadataArtifactPath
            || metadataArtifactDigest is null)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"Manifest host artifact '{Vocabulary.GetText(registeredHost)}' must contain metadata artifact fields.");
        }

        var metadataPathResult = PackagePathResolver.ResolveRegularFile(skillDirectory, metadataArtifactPath);
        if (!metadataPathResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(metadataPathResult.Failure!.Code, metadataPathResult.Failure.Message);
        }

        if (!File.Exists(metadataPathResult.Value!.Value))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        var metadata = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(metadataPathResult.Value!.Value, cancellationToken).ConfigureAwait(false));
        var actualDigest = digestCalculator.ComputeSingleFileDigest(metadataArtifactPath, metadata);
        return SkillOperationResult<bool>.Success(actualDigest == metadataArtifactDigest);
    }

    /// <summary> Determines whether a skill directory is materialized for a supported host other than the requested host. </summary>
    /// <param name="skillDirectory"> The skill directory. </param>
    /// <param name="manifest"> The canonical manifest. </param>
    /// <param name="host"> The requested host. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> <see langword="true" /> when files match another supported host; otherwise <see langword="false" />. </returns>
    public async ValueTask<SkillOperationResult<bool>> MatchesDifferentHostAsync (
        AbsolutePath skillDirectory,
        SkillManifest manifest,
        HostKind host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillDirectory);
        ArgumentNullException.ThrowIfNull(manifest);
        cancellationToken.ThrowIfCancellationRequested();

        var requestedRegistrationResult = HostRegistration.Get(host);
        if (!requestedRegistrationResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(
                requestedRegistrationResult.Failure!.Code,
                requestedRegistrationResult.Failure.Message);
        }

        var requestedHost = requestedRegistrationResult.Value!.Host;
        foreach (var registration in HostRegistration.Registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidateHost = registration.Host;
            if (candidateHost == requestedHost)
            {
                continue;
            }

            var matchResult = await MatchesHostAsync(skillDirectory, manifest, candidateHost, cancellationToken).ConfigureAwait(false);
            if (!matchResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(matchResult.Failure!.Code, matchResult.Failure.Message);
            }

            if (matchResult.Value)
            {
                return SkillOperationResult<bool>.Success(true);
            }
        }

        return SkillOperationResult<bool>.Success(false);
    }

    /// <summary> Extracts YAML frontmatter from a materialized <c>SKILL.md</c>. </summary>
    /// <param name="text"> The SKILL.md text. </param>
    /// <param name="frontmatter"> The extracted frontmatter. </param>
    /// <returns> <see langword="true" /> when frontmatter exists; otherwise <see langword="false" />. </returns>
    public static bool TryExtractFrontmatter (
        string text,
        out string frontmatter)
    {
        ArgumentNullException.ThrowIfNull(text);
        frontmatter = string.Empty;

        var normalized = SkillTextNormalizer.NormalizeToLf(text);
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return false;
        }

        var closingIndex = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (closingIndex < 0)
        {
            return false;
        }

        frontmatter = normalized[..(closingIndex + "\n---\n".Length)];
        return true;
    }
}

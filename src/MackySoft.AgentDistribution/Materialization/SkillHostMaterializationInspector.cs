using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Manifests;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Materialization;

/// <summary> Inspects installed files against the host-specific materialization declared by a SKILL manifest. </summary>
public sealed class SkillHostMaterializationInspector
{
    private readonly PackageContentDigestCalculator digestCalculator;

    /// <summary> Initializes a new instance of the <see cref="SkillHostMaterializationInspector" /> class. </summary>
    /// <param name="digestCalculator"> The digest calculator. </param>
    public SkillHostMaterializationInspector (PackageContentDigestCalculator digestCalculator)
    {
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Determines whether a skill directory is materialized for the requested host. </summary>
    /// <param name="skillDirectory"> The skill directory. </param>
    /// <param name="manifest"> The canonical manifest. </param>
    /// <param name="host"> The requested host. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> <see langword="true" /> when files match the requested host; otherwise <see langword="false" />. </returns>
    public async ValueTask<AgentDistributionOperationResult<bool>> MatchesHostAsync (
        AbsolutePath skillDirectory,
        SkillManifest manifest,
        HostKind host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillDirectory);
        ArgumentNullException.ThrowIfNull(manifest);
        cancellationToken.ThrowIfCancellationRequested();

        var registrationResult = BuiltInHostCatalog.Get(host);
        if (!registrationResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                registrationResult.Failure!.Code,
                registrationResult.Failure.Message);
        }

        var registration = registrationResult.Value!;
        var registeredHost = registration.Host;
        var descriptorMetadataArtifactPath = registration.Skill.MetadataArtifactPath;
        var expectedArtifact = manifest.HostArtifacts.SingleOrDefault(artifact => artifact.Host == registeredHost);
        if (expectedArtifact is null)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.ManifestInvalid,
                $"Manifest does not contain host artifact '{Vocabulary.GetText(registeredHost)}'.");
        }

        var skillPathResult = PackagePathResolver.ResolveRegularFile(skillDirectory, PackageRelativePath.Parse("SKILL.md"));
        if (!skillPathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(skillPathResult.Failure!.Code, skillPathResult.Failure.Message);
        }

        var skillPath = skillPathResult.Value!;
        if (!File.Exists(skillPath.Value))
        {
            return AgentDistributionOperationResult<bool>.Success(false);
        }

        var skillText = AgentDistributionTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(skillPath.Value, cancellationToken).ConfigureAwait(false));
        if (!TryExtractFrontmatter(skillText, out var frontmatter))
        {
            return AgentDistributionOperationResult<bool>.Success(false);
        }

        var actualFrontmatterDigest = digestCalculator.ComputeSingleFileDigest(PackageRelativePath.Parse("SKILL.md.frontmatter"), frontmatter);
        if (actualFrontmatterDigest != expectedArtifact.MaterializedFrontmatterDigest)
        {
            return AgentDistributionOperationResult<bool>.Success(false);
        }

        if (descriptorMetadataArtifactPath is null)
        {
            return expectedArtifact.Path is null && expectedArtifact.Digest is null
                ? AgentDistributionOperationResult<bool>.Success(true)
                : AgentDistributionOperationResult<bool>.FailureResult(
                    AgentDistributionFailureCodes.ManifestInvalid,
                    $"Manifest host artifact '{Vocabulary.GetText(registeredHost)}' must not contain metadata artifact fields.");
        }

        var metadataArtifactPath = expectedArtifact.Path;
        var metadataArtifactDigest = expectedArtifact.Digest;
        if (metadataArtifactPath is null
            || metadataArtifactPath != descriptorMetadataArtifactPath
            || metadataArtifactDigest is null)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.ManifestInvalid,
                $"Manifest host artifact '{Vocabulary.GetText(registeredHost)}' must contain metadata artifact fields.");
        }

        var metadataPathResult = PackagePathResolver.ResolveRegularFile(skillDirectory, metadataArtifactPath);
        if (!metadataPathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(metadataPathResult.Failure!.Code, metadataPathResult.Failure.Message);
        }

        if (!File.Exists(metadataPathResult.Value!.Value))
        {
            return AgentDistributionOperationResult<bool>.Success(false);
        }

        var metadata = AgentDistributionTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(metadataPathResult.Value!.Value, cancellationToken).ConfigureAwait(false));
        var actualDigest = digestCalculator.ComputeSingleFileDigest(metadataArtifactPath, metadata);
        return AgentDistributionOperationResult<bool>.Success(actualDigest == metadataArtifactDigest);
    }

    /// <summary> Determines whether a skill directory is materialized for a supported host other than the requested host. </summary>
    /// <param name="skillDirectory"> The skill directory. </param>
    /// <param name="manifest"> The canonical manifest. </param>
    /// <param name="host"> The requested host. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> <see langword="true" /> when files match another supported host; otherwise <see langword="false" />. </returns>
    public async ValueTask<AgentDistributionOperationResult<bool>> MatchesDifferentHostAsync (
        AbsolutePath skillDirectory,
        SkillManifest manifest,
        HostKind host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillDirectory);
        ArgumentNullException.ThrowIfNull(manifest);
        cancellationToken.ThrowIfCancellationRequested();

        var requestedRegistrationResult = BuiltInHostCatalog.Get(host);
        if (!requestedRegistrationResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                requestedRegistrationResult.Failure!.Code,
                requestedRegistrationResult.Failure.Message);
        }

        var requestedHost = requestedRegistrationResult.Value!.Host;
        foreach (var registration in BuiltInHostCatalog.Registrations)
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
                return AgentDistributionOperationResult<bool>.FailureResult(matchResult.Failure!.Code, matchResult.Failure.Message);
            }

            if (matchResult.Value)
            {
                return AgentDistributionOperationResult<bool>.Success(true);
            }
        }

        return AgentDistributionOperationResult<bool>.Success(false);
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

        var normalized = AgentDistributionTextNormalizer.NormalizeToLf(text);
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

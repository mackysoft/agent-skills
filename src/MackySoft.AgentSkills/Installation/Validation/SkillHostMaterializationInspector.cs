using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Installation.Validation;

/// <summary> Inspects installed files to determine whether they belong to the requested host. </summary>
public sealed class SkillHostMaterializationInspector
{
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillDigestCalculator digestCalculator;

    /// <summary> Initializes a new instance of the <see cref="SkillHostMaterializationInspector" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="digestCalculator"> The digest calculator. </param>
    public SkillHostMaterializationInspector (
        SkillHostAdapterSet hostAdapters,
        SkillDigestCalculator digestCalculator)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Determines whether a skill directory is materialized for the requested host. </summary>
    /// <param name="skillDirectory"> The skill directory. </param>
    /// <param name="manifest"> The canonical manifest. </param>
    /// <param name="host"> The requested host. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> <see langword="true" /> when files match the requested host; otherwise <see langword="false" />. </returns>
    public async ValueTask<SkillOperationResult<bool>> MatchesHostAsync (
        string skillDirectory,
        SkillManifest manifest,
        SkillHostKind host,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(manifest);
        cancellationToken.ThrowIfCancellationRequested();

        var adapterResult = hostAdapters.GetAdapter(host);
        if (!adapterResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(
                adapterResult.Failure!.Code,
                adapterResult.Failure.Message);
        }

        var adapter = adapterResult.Value!;
        var registeredHost = adapter.Descriptor.Host;
        var descriptorMetadataArtifactPath = adapter.Descriptor.MetadataArtifactPath;
        var expectedArtifact = manifest.HostArtifacts.SingleOrDefault(artifact => artifact.Host == registeredHost);
        if (expectedArtifact is null)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"Manifest does not contain host artifact '{ContractLiteralCodec.ToValue(registeredHost)}'.");
        }

        var skillPathResult = SkillPackageRegularFileResolver.ResolvePackageFilePath(skillDirectory, "SKILL.md");
        if (!skillPathResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(skillPathResult.Failure!.Code, skillPathResult.Failure.Message);
        }

        var skillPath = skillPathResult.Value!;
        if (!File.Exists(skillPath))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        var skillText = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(skillPath, cancellationToken).ConfigureAwait(false));
        if (!TryExtractFrontmatter(skillText, out var frontmatter))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        var actualFrontmatterDigest = digestCalculator.ComputeSingleFileDigest("SKILL.md.frontmatter", frontmatter);
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
                    $"Manifest host artifact '{ContractLiteralCodec.ToValue(registeredHost)}' must not contain metadata artifact fields.");
        }

        var metadataArtifactPath = expectedArtifact.Path;
        var metadataArtifactDigest = expectedArtifact.Digest;
        if (metadataArtifactPath is null
            || !string.Equals(metadataArtifactPath, descriptorMetadataArtifactPath, StringComparison.Ordinal)
            || metadataArtifactDigest is null)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"Manifest host artifact '{ContractLiteralCodec.ToValue(registeredHost)}' must contain metadata artifact fields.");
        }

        var metadataPathResult = SkillPackageRegularFileResolver.ResolvePackageFilePath(skillDirectory, metadataArtifactPath);
        if (!metadataPathResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(metadataPathResult.Failure!.Code, metadataPathResult.Failure.Message);
        }

        if (!File.Exists(metadataPathResult.Value!))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        var metadata = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(metadataPathResult.Value!, cancellationToken).ConfigureAwait(false));
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
        string skillDirectory,
        SkillManifest manifest,
        SkillHostKind host,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(manifest);
        cancellationToken.ThrowIfCancellationRequested();

        var requestedAdapterResult = hostAdapters.GetAdapter(host);
        if (!requestedAdapterResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(
                requestedAdapterResult.Failure!.Code,
                requestedAdapterResult.Failure.Message);
        }

        var requestedHost = requestedAdapterResult.Value!.Descriptor.Host;
        foreach (var adapter in hostAdapters.Adapters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidateHost = adapter.Descriptor.Host;
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

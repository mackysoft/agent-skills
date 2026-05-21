using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Validation;

/// <summary> Verifies an installed SKILL package against its own installed manifest. </summary>
public sealed class SkillInstalledPackageIntegrityVerifier
{
    private readonly SkillInstalledManifestReader installedManifestReader;
    private readonly SkillManifestJsonSerializer manifestSerializer;
    private readonly SkillManifestDigestCalculator manifestDigestCalculator;
    private readonly SkillHostMaterializationInspector hostInspector;
    private readonly SkillDigestCalculator digestCalculator;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledPackageIntegrityVerifier" /> class. </summary>
    /// <param name="installedManifestReader"> The installed manifest reader. </param>
    /// <param name="manifestSerializer"> The manifest serializer. </param>
    /// <param name="manifestDigestCalculator"> The canonical manifest digest calculator. </param>
    /// <param name="hostInspector"> The host materialization inspector. </param>
    /// <param name="digestCalculator"> The digest calculator. </param>
    public SkillInstalledPackageIntegrityVerifier (
        SkillInstalledManifestReader installedManifestReader,
        SkillManifestJsonSerializer manifestSerializer,
        SkillManifestDigestCalculator manifestDigestCalculator,
        SkillHostMaterializationInspector hostInspector,
        SkillDigestCalculator digestCalculator)
    {
        this.installedManifestReader = installedManifestReader ?? throw new ArgumentNullException(nameof(installedManifestReader));
        this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
        this.manifestDigestCalculator = manifestDigestCalculator ?? throw new ArgumentNullException(nameof(manifestDigestCalculator));
        this.hostInspector = hostInspector ?? throw new ArgumentNullException(nameof(hostInspector));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Verifies that one installed package is clean and materialized for the requested host. </summary>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="host"> The requested host. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The installed manifest when integrity verification succeeds; otherwise a failure. </returns>
    public async ValueTask<SkillOperationResult<SkillManifest>> VerifyAsync (
        string skillDirectory,
        string host,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        cancellationToken.ThrowIfCancellationRequested();

        var installedManifestResult = await installedManifestReader.ReadRequiredAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!installedManifestResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                installedManifestResult.Failure!.Code,
                installedManifestResult.Failure.Message);
        }

        var installedManifest = installedManifestResult.Value!;
        var manifest = installedManifest.Manifest;
        var manifestIntegrityResult = VerifyInstalledManifestIntegrity(installedManifest);
        if (!manifestIntegrityResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(manifestIntegrityResult.Failure!.Code, manifestIntegrityResult.Failure.Message);
        }

        if (!manifestIntegrityResult.Value!.Matches)
        {
            var failure = manifestIntegrityResult.Value.Failure!;
            return SkillOperationResult<SkillManifest>.FailureResult(
                failure.Code,
                failure.Message);
        }

        var differentHostResult = await hostInspector.MatchesDifferentHostAsync(skillDirectory, manifest, host, cancellationToken).ConfigureAwait(false);
        if (!differentHostResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(differentHostResult.Failure!.Code, differentHostResult.Failure.Message);
        }

        if (differentHostResult.Value)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                SkillFailureCodes.InstallTargetHostConflict,
                $"Installed skill directory is materialized for another host: {skillDirectory}");
        }

        var hostArtifactResult = await VerifyRequestedHostArtifactAsync(skillDirectory, manifest, host, cancellationToken).ConfigureAwait(false);
        if (!hostArtifactResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                hostArtifactResult.Failure!.Code,
                hostArtifactResult.Failure.Message);
        }

        var entriesResult = SkillInstalledFileSetVerifier.ReadInstalledEntries(skillDirectory, cancellationToken);
        if (!entriesResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(entriesResult.Failure!.Code, entriesResult.Failure.Message);
        }

        var installedEntries = entriesResult.Value!;
        var fileSetResult = VerifyInstalledFileSet(skillDirectory, manifest, host, installedEntries, cancellationToken);
        if (!fileSetResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(fileSetResult.Failure!.Code, fileSetResult.Failure.Message);
        }

        if (fileSetResult.Value!.HasFileSetDrift)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                SkillFailureCodes.InstallTargetFileSetMismatch,
                $"Installed SKILL file set contains unmanaged files: {manifest.SkillName}");
        }

        var frontmatterResult = await VerifyRequestedHostFrontmatterAsync(skillDirectory, manifest, host, cancellationToken).ConfigureAwait(false);
        if (!frontmatterResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                frontmatterResult.Failure!.Code,
                frontmatterResult.Failure.Message);
        }

        var digestResult = await VerifyInstalledContentDigestAsync(skillDirectory, manifest, installedEntries, cancellationToken).ConfigureAwait(false);
        if (!digestResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(digestResult.Failure!.Code, digestResult.Failure.Message);
        }

        return !digestResult.Value
            ? SkillOperationResult<SkillManifest>.FailureResult(
                SkillFailureCodes.InstallTargetContentDigestMismatch,
                $"Installed SKILL files do not match installed contentDigest: {manifest.SkillName}")
            : SkillOperationResult<SkillManifest>.Success(manifest);
    }

    private SkillOperationResult<IntegrityCheckResult> VerifyInstalledManifestIntegrity (SkillInstalledManifest installedManifest)
    {
        if (!string.Equals(installedManifest.ManifestText, manifestSerializer.Serialize(installedManifest.Manifest), StringComparison.Ordinal))
        {
            return SkillOperationResult<IntegrityCheckResult>.Success(IntegrityCheckResult.Mismatch(
                SkillFailureCodes.InstallTargetManifestDigestMismatch,
                $"Installed SKILL manifest text is not canonical: {installedManifest.Manifest.SkillName}"));
        }

        var manifest = installedManifest.Manifest;
        var manifestDigest = manifestDigestCalculator.ComputeManifestDigest(manifest);
        if (!string.Equals(manifestDigest, manifest.ManifestDigest, StringComparison.Ordinal))
        {
            return SkillOperationResult<IntegrityCheckResult>.Success(IntegrityCheckResult.Mismatch(
                SkillFailureCodes.InstallTargetManifestDigestMismatch,
                $"Installed SKILL manifestDigest does not match manifest content: {manifest.SkillName}"));
        }

        return SkillOperationResult<IntegrityCheckResult>.Success(IntegrityCheckResult.Match);
    }

    private async ValueTask<SkillOperationResult<bool>> VerifyRequestedHostArtifactAsync (
        string skillDirectory,
        SkillManifest manifest,
        string host,
        CancellationToken cancellationToken)
    {
        var hostArtifact = manifest.HostArtifacts.SingleOrDefault(artifact => string.Equals(artifact.Host, host, StringComparison.Ordinal));
        if (hostArtifact is null)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"Manifest does not contain host artifact '{host}'.");
        }

        var hostArtifactResult = await MatchesHostArtifactAsync(skillDirectory, hostArtifact, cancellationToken).ConfigureAwait(false);
        if (!hostArtifactResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(hostArtifactResult.Failure!.Code, hostArtifactResult.Failure.Message);
        }

        return hostArtifactResult.Value
            ? SkillOperationResult<bool>.Success(true)
            : SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetHostArtifactDigestMismatch,
                $"Installed SKILL host artifact digest does not match manifest: {manifest.SkillName}");
    }

    private async ValueTask<SkillOperationResult<bool>> VerifyRequestedHostFrontmatterAsync (
        string skillDirectory,
        SkillManifest manifest,
        string host,
        CancellationToken cancellationToken)
    {
        var hostArtifact = manifest.HostArtifacts.SingleOrDefault(artifact => string.Equals(artifact.Host, host, StringComparison.Ordinal));
        if (hostArtifact is null)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"Manifest does not contain host artifact '{host}'.");
        }

        var frontmatterResult = await ReadInstalledFrontmatterAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!frontmatterResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(frontmatterResult.Failure!.Code, frontmatterResult.Failure.Message);
        }

        if (frontmatterResult.Value!.Length == 0)
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetFrontmatterDigestMismatch,
                $"Installed SKILL frontmatter is missing or invalid: {manifest.SkillName}");
        }

        var frontmatterDigest = digestCalculator.ComputeSingleFileDigest("SKILL.md.frontmatter", frontmatterResult.Value);
        if (!string.Equals(frontmatterDigest, hostArtifact.MaterializedFrontmatterDigest, StringComparison.Ordinal))
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.InstallTargetFrontmatterDigestMismatch,
                $"Installed SKILL frontmatter digest does not match manifest: {manifest.SkillName}");
        }

        return SkillOperationResult<bool>.Success(true);
    }

    private static async ValueTask<SkillOperationResult<string>> ReadInstalledFrontmatterAsync (
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var skillPathResult = SkillPackageRegularFileResolver.ResolvePackageFilePath(skillDirectory, SkillManagedFileSetPaths.SkillBodyPath);
        if (!skillPathResult.IsSuccess)
        {
            return SkillOperationResult<string>.FailureResult(skillPathResult.Failure!.Code, skillPathResult.Failure.Message);
        }

        if (!File.Exists(skillPathResult.Value!))
        {
            return SkillOperationResult<string>.Success(string.Empty);
        }

        var skillText = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(skillPathResult.Value!, cancellationToken).ConfigureAwait(false));
        return SkillHostMaterializationInspector.TryExtractFrontmatter(skillText, out var frontmatter)
            ? SkillOperationResult<string>.Success(frontmatter)
            : SkillOperationResult<string>.Success(string.Empty);
    }

    private async ValueTask<SkillOperationResult<bool>> MatchesHostArtifactAsync (
        string skillDirectory,
        SkillHostArtifactManifest hostArtifact,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hostArtifact.Path))
        {
            return SkillOperationResult<bool>.Success(true);
        }

        if (string.IsNullOrWhiteSpace(hostArtifact.Digest))
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"Manifest host artifact '{hostArtifact.Host}' is missing a digest.");
        }

        var artifactPathResult = SkillPackageRegularFileResolver.ResolvePackageFilePath(skillDirectory, hostArtifact.Path);
        if (!artifactPathResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(artifactPathResult.Failure!.Code, artifactPathResult.Failure.Message);
        }

        if (!File.Exists(artifactPathResult.Value!))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        var content = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(artifactPathResult.Value!, cancellationToken).ConfigureAwait(false));
        var digest = digestCalculator.ComputeSingleFileDigest(hostArtifact.Path, content);
        return SkillOperationResult<bool>.Success(string.Equals(digest, hostArtifact.Digest, StringComparison.Ordinal));
    }

    private async ValueTask<SkillOperationResult<bool>> VerifyInstalledContentDigestAsync (
        string skillDirectory,
        SkillManifest manifest,
        SkillInstalledFileSetVerifier.SkillInstalledFileSetEntries installedEntries,
        CancellationToken cancellationToken)
    {
        var digestInputResult = await ReadInstalledDigestInputsAsync(skillDirectory, installedEntries, cancellationToken).ConfigureAwait(false);
        if (!digestInputResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(digestInputResult.Failure!.Code, digestInputResult.Failure.Message);
        }

        var actualDigest = digestCalculator.ComputeDigest(digestInputResult.Value!);
        return SkillOperationResult<bool>.Success(string.Equals(actualDigest, manifest.ContentDigest, StringComparison.Ordinal));
    }

    private static async ValueTask<SkillOperationResult<IReadOnlyList<SkillDigestInputFile>>> ReadInstalledDigestInputsAsync (
        string skillDirectory,
        SkillInstalledFileSetVerifier.SkillInstalledFileSetEntries installedEntries,
        CancellationToken cancellationToken)
    {
        var skillBodyResult = await ReadInstalledSkillBodyAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!skillBodyResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<SkillDigestInputFile>>.FailureResult(
                skillBodyResult.Failure!.Code,
                skillBodyResult.Failure.Message);
        }

        if (!skillBodyResult.Value.Exists)
        {
            return SkillOperationResult<IReadOnlyList<SkillDigestInputFile>>.Success(Array.Empty<SkillDigestInputFile>());
        }

        var digestInputs = new List<SkillDigestInputFile>
        {
            new(SkillManagedFileSetPaths.SkillBodyPath, skillBodyResult.Value.Body),
        };

        foreach (var relativePath in installedEntries.Files
            .Where(static path => path.StartsWith(SkillManagedFileSetPaths.ReferencesPrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resolvedPathResult = SkillPackageRegularFileResolver.ResolvePackageFilePath(skillDirectory, relativePath);
            if (!resolvedPathResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<SkillDigestInputFile>>.FailureResult(
                    resolvedPathResult.Failure!.Code,
                    resolvedPathResult.Failure.Message);
            }

            var content = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(resolvedPathResult.Value!, cancellationToken).ConfigureAwait(false));
            digestInputs.Add(new SkillDigestInputFile(relativePath, content));
        }

        return SkillOperationResult<IReadOnlyList<SkillDigestInputFile>>.Success(digestInputs);
    }

    private static async ValueTask<SkillOperationResult<InstalledSkillBody>> ReadInstalledSkillBodyAsync (
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var skillPathResult = SkillPackageRegularFileResolver.ResolvePackageFilePath(skillDirectory, SkillManagedFileSetPaths.SkillBodyPath);
        if (!skillPathResult.IsSuccess)
        {
            return SkillOperationResult<InstalledSkillBody>.FailureResult(skillPathResult.Failure!.Code, skillPathResult.Failure.Message);
        }

        if (!File.Exists(skillPathResult.Value!))
        {
            return SkillOperationResult<InstalledSkillBody>.Success(InstalledSkillBody.Missing);
        }

        var skillText = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(skillPathResult.Value!, cancellationToken).ConfigureAwait(false));
        if (!SkillHostMaterializationInspector.TryExtractFrontmatter(skillText, out var frontmatter))
        {
            return SkillOperationResult<InstalledSkillBody>.Success(InstalledSkillBody.Missing);
        }

        var body = skillText[frontmatter.Length..];
        if (body.StartsWith('\n'))
        {
            body = body[1..];
        }

        return SkillOperationResult<InstalledSkillBody>.Success(new InstalledSkillBody(true, body));
    }

    private static SkillOperationResult<SkillInstalledFileSetVerificationResult> VerifyInstalledFileSet (
        string skillDirectory,
        SkillManifest manifest,
        string host,
        SkillInstalledFileSetVerifier.SkillInstalledFileSetEntries installedEntries,
        CancellationToken cancellationToken)
    {
        var requiredPathsResult = SkillManagedFileSetPaths.CreateInstalledManifestRequiredPaths(manifest, host);
        if (!requiredPathsResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledFileSetVerificationResult>.FailureResult(
                requiredPathsResult.Failure!.Code,
                requiredPathsResult.Failure.Message);
        }

        return SkillInstalledFileSetVerifier.VerifyInstalledEntries(
            skillDirectory,
            requiredPathsResult.Value!,
            [SkillManagedFileSetPaths.ReferencesPrefix],
            installedEntries,
            cancellationToken);
    }

    private sealed record IntegrityCheckResult (
        bool Matches,
        SkillFailure? Failure)
    {
        public static IntegrityCheckResult Match { get; } = new(
            true,
            null);

        public static IntegrityCheckResult Mismatch (
            SkillFailureCode failureCode,
            string message)
        {
            return new IntegrityCheckResult(false, SkillFailure.Create(failureCode, message));
        }
    }

    private readonly record struct InstalledSkillBody (
        bool Exists,
        string Body)
    {
        public static InstalledSkillBody Missing { get; } = new(false, string.Empty);
    }
}

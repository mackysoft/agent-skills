using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Validation;

/// <summary> Verifies an installed SKILL package against its own installed manifest. </summary>
public sealed class SkillInstalledPackageIntegrityVerifier
{
    private readonly SkillInstalledManifestReader installedManifestReader;
    private readonly SkillManifestJsonSerializer manifestSerializer;
    private readonly SkillHostMaterializationInspector hostInspector;
    private readonly PackageContentDigestCalculator digestCalculator;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledPackageIntegrityVerifier" /> class. </summary>
    /// <param name="installedManifestReader"> The installed manifest reader. </param>
    /// <param name="manifestSerializer"> The manifest serializer. </param>
    /// <param name="hostInspector"> The host materialization inspector. </param>
    /// <param name="digestCalculator"> The digest calculator. </param>
    public SkillInstalledPackageIntegrityVerifier (
        SkillInstalledManifestReader installedManifestReader,
        SkillManifestJsonSerializer manifestSerializer,
        SkillHostMaterializationInspector hostInspector,
        PackageContentDigestCalculator digestCalculator)
    {
        this.installedManifestReader = installedManifestReader ?? throw new ArgumentNullException(nameof(installedManifestReader));
        this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
        this.hostInspector = hostInspector ?? throw new ArgumentNullException(nameof(hostInspector));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Verifies that one installed package is clean and materialized for the requested host. </summary>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="host"> The requested host. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The installed manifest when integrity verification succeeds; otherwise a failure. </returns>
    public async ValueTask<AgentDistributionOperationResult<SkillManifest>> VerifyAsync (
        AbsolutePath skillDirectory,
        HostKind host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var installedManifestResult = await installedManifestReader.ReadRequiredAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!installedManifestResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillManifest>.FailureResult(
                installedManifestResult.Failure!.Code,
                installedManifestResult.Failure.Message);
        }

        var installedManifest = installedManifestResult.Value!;
        var manifest = installedManifest.Manifest;
        var manifestIntegrityResult = VerifyInstalledManifestIntegrity(installedManifest);
        if (!manifestIntegrityResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillManifest>.FailureResult(manifestIntegrityResult.Failure!.Code, manifestIntegrityResult.Failure.Message);
        }

        if (!manifestIntegrityResult.Value!.Matches)
        {
            var failure = manifestIntegrityResult.Value.Failure!;
            return AgentDistributionOperationResult<SkillManifest>.FailureResult(
                failure.Code,
                failure.Message);
        }

        var differentHostResult = await hostInspector.MatchesDifferentHostAsync(skillDirectory, manifest, host, cancellationToken).ConfigureAwait(false);
        if (!differentHostResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillManifest>.FailureResult(differentHostResult.Failure!.Code, differentHostResult.Failure.Message);
        }

        if (differentHostResult.Value)
        {
            return AgentDistributionOperationResult<SkillManifest>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetHostConflict,
                $"Installed skill directory is materialized for another host: {skillDirectory}");
        }

        var hostArtifactResult = await VerifyRequestedHostArtifactAsync(skillDirectory, manifest, host, cancellationToken).ConfigureAwait(false);
        if (!hostArtifactResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillManifest>.FailureResult(
                hostArtifactResult.Failure!.Code,
                hostArtifactResult.Failure.Message);
        }

        var entriesResult = SkillInstalledFileSetVerifier.ReadInstalledEntries(skillDirectory, cancellationToken);
        if (!entriesResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillManifest>.FailureResult(entriesResult.Failure!.Code, entriesResult.Failure.Message);
        }

        var installedEntries = entriesResult.Value!;
        var fileSetResult = VerifyInstalledFileSet(skillDirectory, manifest, host, installedEntries, cancellationToken);
        if (!fileSetResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillManifest>.FailureResult(fileSetResult.Failure!.Code, fileSetResult.Failure.Message);
        }

        if (fileSetResult.Value!.HasFileSetDrift)
        {
            return AgentDistributionOperationResult<SkillManifest>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetFileSetMismatch,
                $"Installed SKILL file set contains unmanaged files: {manifest.SkillName}");
        }

        var frontmatterResult = await VerifyRequestedHostFrontmatterAsync(skillDirectory, manifest, host, cancellationToken).ConfigureAwait(false);
        if (!frontmatterResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillManifest>.FailureResult(
                frontmatterResult.Failure!.Code,
                frontmatterResult.Failure.Message);
        }

        var digestResult = await VerifyInstalledContentDigestAsync(skillDirectory, manifest, installedEntries, cancellationToken).ConfigureAwait(false);
        if (!digestResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillManifest>.FailureResult(digestResult.Failure!.Code, digestResult.Failure.Message);
        }

        return !digestResult.Value
            ? AgentDistributionOperationResult<SkillManifest>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetContentDigestMismatch,
                $"Installed SKILL files do not match installed contentDigest: {manifest.SkillName}")
            : AgentDistributionOperationResult<SkillManifest>.Success(manifest);
    }

    private AgentDistributionOperationResult<IntegrityCheckResult> VerifyInstalledManifestIntegrity (SkillInstalledManifest installedManifest)
    {
        if (!IsCanonicalManifestText(installedManifest))
        {
            return AgentDistributionOperationResult<IntegrityCheckResult>.Success(IntegrityCheckResult.Mismatch(
                AgentDistributionFailureCodes.InstallTargetManifestDigestMismatch,
                $"Installed SKILL manifest text is not canonical: {installedManifest.Manifest.SkillName}"));
        }

        return AgentDistributionOperationResult<IntegrityCheckResult>.Success(IntegrityCheckResult.Match);
    }

    private bool IsCanonicalManifestText (SkillInstalledManifest installedManifest)
    {
        return string.Equals(installedManifest.ManifestText, manifestSerializer.Serialize(installedManifest.Manifest), StringComparison.Ordinal);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> VerifyRequestedHostArtifactAsync (
        AbsolutePath skillDirectory,
        SkillManifest manifest,
        HostKind host,
        CancellationToken cancellationToken)
    {
        var hostArtifact = manifest.HostArtifacts.SingleOrDefault(artifact => artifact.Host == host);
        if (hostArtifact is null)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.ManifestInvalid,
                $"Manifest does not contain host artifact '{Vocabulary.GetText(host)}'.");
        }

        var hostArtifactResult = await MatchesHostArtifactAsync(skillDirectory, hostArtifact, cancellationToken).ConfigureAwait(false);
        if (!hostArtifactResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(hostArtifactResult.Failure!.Code, hostArtifactResult.Failure.Message);
        }

        return hostArtifactResult.Value
            ? AgentDistributionOperationResult<bool>.Success(true)
            : AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetHostArtifactDigestMismatch,
                $"Installed SKILL host artifact digest does not match manifest: {manifest.SkillName}");
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> VerifyRequestedHostFrontmatterAsync (
        AbsolutePath skillDirectory,
        SkillManifest manifest,
        HostKind host,
        CancellationToken cancellationToken)
    {
        var hostArtifact = manifest.HostArtifacts.SingleOrDefault(artifact => artifact.Host == host);
        if (hostArtifact is null)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.ManifestInvalid,
                $"Manifest does not contain host artifact '{Vocabulary.GetText(host)}'.");
        }

        var frontmatterResult = await ReadInstalledFrontmatterAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!frontmatterResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(frontmatterResult.Failure!.Code, frontmatterResult.Failure.Message);
        }

        if (frontmatterResult.Value!.Length == 0)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetFrontmatterDigestMismatch,
                $"Installed SKILL frontmatter is missing or invalid: {manifest.SkillName}");
        }

        var frontmatterDigest = digestCalculator.ComputeSingleFileDigest(PackageRelativePath.Parse("SKILL.md.frontmatter"), frontmatterResult.Value);
        if (frontmatterDigest != hostArtifact.MaterializedFrontmatterDigest)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetFrontmatterDigestMismatch,
                $"Installed SKILL frontmatter digest does not match manifest: {manifest.SkillName}");
        }

        return AgentDistributionOperationResult<bool>.Success(true);
    }

    private static async ValueTask<AgentDistributionOperationResult<string>> ReadInstalledFrontmatterAsync (
        AbsolutePath skillDirectory,
        CancellationToken cancellationToken)
    {
        var skillPathResult = PackagePathResolver.ResolveRegularFile(skillDirectory, SkillManagedFileSetPaths.SkillBodyPath);
        if (!skillPathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<string>.FailureResult(skillPathResult.Failure!.Code, skillPathResult.Failure.Message);
        }

        if (!File.Exists(skillPathResult.Value!.Value))
        {
            return AgentDistributionOperationResult<string>.Success(string.Empty);
        }

        var skillText = AgentDistributionTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(skillPathResult.Value.Value, cancellationToken).ConfigureAwait(false));
        return SkillHostMaterializationInspector.TryExtractFrontmatter(skillText, out var frontmatter)
            ? AgentDistributionOperationResult<string>.Success(frontmatter)
            : AgentDistributionOperationResult<string>.Success(string.Empty);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> MatchesHostArtifactAsync (
        AbsolutePath skillDirectory,
        SkillHostArtifactManifest hostArtifact,
        CancellationToken cancellationToken)
    {
        if (hostArtifact.Path is null)
        {
            return AgentDistributionOperationResult<bool>.Success(true);
        }

        if (hostArtifact.Digest is null)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.ManifestInvalid,
                $"Manifest host artifact '{hostArtifact.Host}' is missing a digest.");
        }

        var artifactPathResult = PackagePathResolver.ResolveRegularFile(skillDirectory, hostArtifact.Path);
        if (!artifactPathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(artifactPathResult.Failure!.Code, artifactPathResult.Failure.Message);
        }

        if (!File.Exists(artifactPathResult.Value!.Value))
        {
            return AgentDistributionOperationResult<bool>.Success(false);
        }

        var content = AgentDistributionTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(artifactPathResult.Value.Value, cancellationToken).ConfigureAwait(false));
        var digest = digestCalculator.ComputeSingleFileDigest(hostArtifact.Path, content);
        return AgentDistributionOperationResult<bool>.Success(digest == hostArtifact.Digest);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> VerifyInstalledContentDigestAsync (
        AbsolutePath skillDirectory,
        SkillManifest manifest,
        SkillInstalledFileSetVerifier.SkillInstalledFileSetEntries installedEntries,
        CancellationToken cancellationToken)
    {
        var digestInputResult = await ReadInstalledDigestInputsAsync(skillDirectory, installedEntries, cancellationToken).ConfigureAwait(false);
        if (!digestInputResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(digestInputResult.Failure!.Code, digestInputResult.Failure.Message);
        }

        var actualDigest = digestCalculator.ComputeDigest(digestInputResult.Value!);
        return AgentDistributionOperationResult<bool>.Success(actualDigest == manifest.ContentDigest);
    }

    private static async ValueTask<AgentDistributionOperationResult<IReadOnlyList<PackageContentDigestInputFile>>> ReadInstalledDigestInputsAsync (
        AbsolutePath skillDirectory,
        SkillInstalledFileSetVerifier.SkillInstalledFileSetEntries installedEntries,
        CancellationToken cancellationToken)
    {
        var skillBodyResult = await ReadInstalledSkillBodyAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!skillBodyResult.IsSuccess)
        {
            return AgentDistributionOperationResult<IReadOnlyList<PackageContentDigestInputFile>>.FailureResult(
                skillBodyResult.Failure!.Code,
                skillBodyResult.Failure.Message);
        }

        var skillBody = skillBodyResult.Value!;
        if (!skillBody.Exists)
        {
            return AgentDistributionOperationResult<IReadOnlyList<PackageContentDigestInputFile>>.Success(Array.Empty<PackageContentDigestInputFile>());
        }

        var digestInputs = new List<PackageContentDigestInputFile>
        {
            new(SkillManagedFileSetPaths.SkillBodyPath, skillBody.Body),
        };

        foreach (var relativePath in installedEntries.Files
            .Where(SkillContentFileSetPaths.IsSupplementalContentFile)
            .OrderBy(static path => path.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resolvedPathResult = PackagePathResolver.ResolveRegularFile(skillDirectory, relativePath);
            if (!resolvedPathResult.IsSuccess)
            {
                return AgentDistributionOperationResult<IReadOnlyList<PackageContentDigestInputFile>>.FailureResult(
                    resolvedPathResult.Failure!.Code,
                    resolvedPathResult.Failure.Message);
            }

            var content = AgentDistributionTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(resolvedPathResult.Value!.Value, cancellationToken).ConfigureAwait(false));
            digestInputs.Add(new PackageContentDigestInputFile(relativePath, content));
        }

        return AgentDistributionOperationResult<IReadOnlyList<PackageContentDigestInputFile>>.Success(digestInputs);
    }

    private static async ValueTask<AgentDistributionOperationResult<InstalledSkillBody>> ReadInstalledSkillBodyAsync (
        AbsolutePath skillDirectory,
        CancellationToken cancellationToken)
    {
        var skillPathResult = PackagePathResolver.ResolveRegularFile(skillDirectory, SkillManagedFileSetPaths.SkillBodyPath);
        if (!skillPathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<InstalledSkillBody>.FailureResult(skillPathResult.Failure!.Code, skillPathResult.Failure.Message);
        }

        if (!File.Exists(skillPathResult.Value!.Value))
        {
            return AgentDistributionOperationResult<InstalledSkillBody>.Success(InstalledSkillBody.Missing);
        }

        var skillText = AgentDistributionTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(skillPathResult.Value.Value, cancellationToken).ConfigureAwait(false));
        if (!SkillHostMaterializationInspector.TryExtractFrontmatter(skillText, out var frontmatter))
        {
            return AgentDistributionOperationResult<InstalledSkillBody>.Success(InstalledSkillBody.Missing);
        }

        var body = skillText[frontmatter.Length..];
        if (body.StartsWith('\n'))
        {
            body = body[1..];
        }

        return AgentDistributionOperationResult<InstalledSkillBody>.Success(InstalledSkillBody.Present(body));
    }

    private static AgentDistributionOperationResult<SkillInstalledFileSetVerificationResult> VerifyInstalledFileSet (
        AbsolutePath skillDirectory,
        SkillManifest manifest,
        HostKind host,
        SkillInstalledFileSetVerifier.SkillInstalledFileSetEntries installedEntries,
        CancellationToken cancellationToken)
    {
        var requiredPathsResult = SkillManagedFileSetPaths.CreateInstalledManifestRequiredPaths(manifest, host);
        if (!requiredPathsResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillInstalledFileSetVerificationResult>.FailureResult(
                requiredPathsResult.Failure!.Code,
                requiredPathsResult.Failure.Message);
        }

        return SkillInstalledFileSetVerifier.VerifyInstalledEntries(
            skillDirectory,
            requiredPathsResult.Value!,
            [
                SkillManagedFileSetPaths.ReferencesDirectoryPath,
                SkillManagedFileSetPaths.ScriptsDirectoryPath,
            ],
            installedEntries,
            cancellationToken);
    }

    private sealed class IntegrityCheckResult
    {
        private IntegrityCheckResult (
            bool matches,
            AgentDistributionFailure? failure)
        {
            if (matches == (failure is not null))
            {
                throw new ArgumentException("A matching integrity result must not contain a failure, and a mismatch must contain one.", nameof(failure));
            }

            Matches = matches;
            Failure = failure;
        }

        public bool Matches { get; }

        public AgentDistributionFailure? Failure { get; }

        public static IntegrityCheckResult Match { get; } = new(true, null);

        public static IntegrityCheckResult Mismatch (
            AgentDistributionFailureCode failureCode,
            string message)
        {
            return new IntegrityCheckResult(false, AgentDistributionFailure.Create(failureCode, message));
        }
    }

    private sealed class InstalledSkillBody
    {
        private InstalledSkillBody (
            bool exists,
            string body)
        {
            ArgumentNullException.ThrowIfNull(body);
            if (!exists && body.Length != 0)
            {
                throw new ArgumentException("A missing installed SKILL body must be empty.", nameof(body));
            }

            Exists = exists;
            Body = body;
        }

        public bool Exists { get; }

        public string Body { get; }

        public static InstalledSkillBody Missing { get; } = new(false, string.Empty);

        public static InstalledSkillBody Present (string body)
        {
            return new InstalledSkillBody(true, body);
        }
    }
}

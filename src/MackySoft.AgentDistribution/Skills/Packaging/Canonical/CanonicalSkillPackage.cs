using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Manifests;

namespace MackySoft.AgentDistribution.Skills.Packaging.Canonical;

/// <summary> Represents one immutable SKILL package with disjoint host-independent and host artifact paths and a self-consistent manifest, file set, and digests. </summary>
/// <remarks> Compatibility with the current built-in host adapters is validated when the package is materialized for a host. </remarks>
public sealed class CanonicalSkillPackage
{
    private static readonly PackageRelativePath ManifestPath = PackageRelativePath.Parse("agent-skill.json");

    /// <summary> Initializes one canonical package from a fully validated candidate. </summary>
    /// <param name="candidate"> The candidate whose manifest, file set, and digests agree. </param>
    private CanonicalSkillPackage (CanonicalSkillPackageCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        Manifest = candidate.Manifest;
        Files = Array.AsReadOnly(candidate.Files
            .OrderBy(static file => file.RelativePath.Value, StringComparer.Ordinal)
            .ToArray());
    }

    /// <summary> Gets the canonical manifest. </summary>
    public SkillManifest Manifest { get; }

    /// <summary> Gets an immutable snapshot of the canonical package files. </summary>
    public IReadOnlyList<PackageTextFile> Files { get; }

    /// <summary> Validates disjoint host-independent and host artifact paths, the package file set, and its digests. </summary>
    public sealed class Factory
    {
        private readonly PackageContentDigestCalculator digestCalculator;
        private readonly SkillManifestJsonSerializer manifestSerializer;

        /// <summary> Initializes the canonical package construction boundary. </summary>
        /// <param name="digestCalculator"> The canonical file digest calculator. </param>
        /// <param name="manifestSerializer"> The canonical manifest serializer. </param>
        public Factory (
            PackageContentDigestCalculator digestCalculator,
            SkillManifestJsonSerializer manifestSerializer)
        {
            this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
            this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
        }

        /// <summary> Validates one complete candidate and creates its canonical package snapshot. </summary>
        internal AgentDistributionOperationResult<CanonicalSkillPackage> CreateCanonical (CanonicalSkillPackageCandidate candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            var validationResult = Validate(candidate.Manifest, candidate.Files);
            return validationResult.IsSuccess
                ? AgentDistributionOperationResult<CanonicalSkillPackage>.Success(new CanonicalSkillPackage(candidate))
                : Failure(validationResult.Failure!.Message);
        }

        private AgentDistributionOperationResult<bool> Validate (
            SkillManifest manifest,
            IReadOnlyList<PackageTextFile> files)
        {
            var portablePaths = new HashSet<PackageRelativePath>(PackageRelativePath.PortableFileSystemComparer);
            foreach (var file in files)
            {
                if (!portablePaths.Add(file.RelativePath))
                {
                    return BoolFailure($"Canonical SKILL package file paths must be unique when case is ignored: {file.RelativePath}");
                }
            }

            var filesByPath = files.ToDictionary(static file => file.RelativePath);
            if (!filesByPath.TryGetValue(ManifestPath, out var manifestFile))
            {
                return BoolFailure($"Generated SKILL package is missing agent-skill.json: {manifest.SkillName}");
            }

            if (!string.Equals(manifestFile.Content, manifestSerializer.Serialize(manifest), StringComparison.Ordinal))
            {
                return BoolFailure($"agent-skill.json is not canonical or does not match the in-memory manifest: {manifest.SkillName}");
            }

            var fileSetResult = ValidateFileSet(filesByPath, manifest);
            if (!fileSetResult.IsSuccess)
            {
                return fileSetResult;
            }

            return ValidateDigests(filesByPath, manifest);
        }

        private static AgentDistributionOperationResult<bool> ValidateFileSet (
            IReadOnlyDictionary<PackageRelativePath, PackageTextFile> filesByPath,
            SkillManifest manifest)
        {
            if (!filesByPath.ContainsKey(SkillContentFileSetPaths.SkillBodyPath))
            {
                return BoolFailure($"Generated SKILL package is missing SKILL.md: {manifest.SkillName}");
            }

            var hostArtifactPaths = manifest.HostArtifacts
                .Select(static artifact => artifact.Path)
                .OfType<PackageRelativePath>()
                .ToHashSet();

            foreach (var hostArtifactPath in hostArtifactPaths)
            {
                if (hostArtifactPath == ManifestPath
                    || SkillContentFileSetPaths.IsContentFile(hostArtifactPath))
                {
                    return BoolFailure($"Generated SKILL host artifact path overlaps host-independent package file: {manifest.SkillName}/{hostArtifactPath}");
                }
            }

            foreach (var relativePath in filesByPath.Keys)
            {
                if (relativePath == SkillContentFileSetPaths.SkillBodyPath
                    || relativePath == ManifestPath
                    || SkillContentFileSetPaths.IsSupplementalContentFile(relativePath)
                    || hostArtifactPaths.Contains(relativePath))
                {
                    continue;
                }

                return BoolFailure($"Generated SKILL package contains an unsupported file: {manifest.SkillName}/{relativePath}");
            }

            return AgentDistributionOperationResult<bool>.Success(true);
        }

        private AgentDistributionOperationResult<bool> ValidateDigests (
            IReadOnlyDictionary<PackageRelativePath, PackageTextFile> filesByPath,
            SkillManifest manifest)
        {
            var contentDigest = digestCalculator.ComputeDigest(filesByPath.Values
                .Where(static file => SkillContentFileSetPaths.IsContentFile(file.RelativePath))
                .Select(static file => new PackageContentDigestInputFile(file.RelativePath, file.Content)));

            if (contentDigest != manifest.ContentDigest)
            {
                return BoolFailure($"Generated SKILL contentDigest does not match files: {manifest.SkillName}");
            }

            foreach (var artifact in manifest.HostArtifacts)
            {
                var metadataArtifactPath = artifact.Path;
                if (metadataArtifactPath is null)
                {
                    continue;
                }

                if (!filesByPath.TryGetValue(metadataArtifactPath, out var artifactFile))
                {
                    return BoolFailure($"Generated SKILL package is missing host artifact: {manifest.SkillName}/{metadataArtifactPath.Value}");
                }

                var artifactDigest = digestCalculator.ComputeSingleFileDigest(metadataArtifactPath, artifactFile.Content);
                if (artifactDigest != artifact.Digest)
                {
                    return BoolFailure($"Generated SKILL host artifact digest does not match files: {manifest.SkillName}/{metadataArtifactPath.Value}");
                }
            }

            return AgentDistributionOperationResult<bool>.Success(true);
        }

        private static AgentDistributionOperationResult<CanonicalSkillPackage> Failure (string message)
        {
            return AgentDistributionOperationResult<CanonicalSkillPackage>.FailureResult(AgentDistributionFailureCodes.ManifestInvalid, message);
        }

        private static AgentDistributionOperationResult<bool> BoolFailure (string message)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(AgentDistributionFailureCodes.ManifestInvalid, message);
        }
    }
}

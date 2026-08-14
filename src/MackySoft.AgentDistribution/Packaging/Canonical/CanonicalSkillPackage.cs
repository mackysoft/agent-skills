using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Packaging.Canonical;

/// <summary> Represents one immutable canonical host-independent SKILL package snapshot. </summary>
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

    /// <summary> Validates complete package candidates and creates canonical package snapshots. </summary>
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
                .Where(static path => path is not null)
                .ToHashSet();

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

            var metadata = new SkillHostMetadata(manifest.SkillName, manifest.DisplayName, manifest.Description);
            var artifactByHost = manifest.HostArtifacts.ToDictionary(static artifact => artifact.Host);
            foreach (var registration in HostRegistration.Registrations)
            {
                var artifact = artifactByHost[registration.Host];
                var adapter = registration.SkillAdapter;
                var hostArtifacts = adapter.BuildArtifacts(metadata);
                var metadataArtifactPath = registration.Skill.MetadataArtifactPath;
                var frontmatterDigest = digestCalculator.ComputeSingleFileDigest(PackageRelativePath.Parse("SKILL.md.frontmatter"), hostArtifacts.Frontmatter);
                if (frontmatterDigest != artifact.MaterializedFrontmatterDigest)
                {
                    return BoolFailure($"Generated SKILL host frontmatter digest does not match adapter output: {manifest.SkillName}/{Vocabulary.GetText(artifact.Host)}");
                }

                if (metadataArtifactPath is null)
                {
                    continue;
                }

                if (!filesByPath.TryGetValue(metadataArtifactPath, out var artifactFile))
                {
                    return BoolFailure($"Generated SKILL package is missing host artifact: {manifest.SkillName}/{metadataArtifactPath.Value}");
                }

                if (hostArtifacts.MetadataContent is null)
                {
                    return BoolFailure($"Generated SKILL host artifact adapter output is missing: {manifest.SkillName}/{metadataArtifactPath.Value}");
                }

                var expectedArtifactDigest = digestCalculator.ComputeSingleFileDigest(metadataArtifactPath, hostArtifacts.MetadataContent);
                if (expectedArtifactDigest != artifact.Digest)
                {
                    return BoolFailure($"Generated SKILL host artifact digest does not match adapter output: {manifest.SkillName}/{metadataArtifactPath.Value}");
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

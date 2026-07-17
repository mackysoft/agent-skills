using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Packaging.Canonical;

/// <summary> Represents one immutable canonical host-independent SKILL package snapshot. </summary>
public sealed class CanonicalSkillPackage
{
    /// <summary> Initializes one canonical package from a fully validated candidate. </summary>
    /// <param name="candidate"> The candidate whose manifest, file set, and digests agree. </param>
    private CanonicalSkillPackage (CanonicalSkillPackageCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        Manifest = candidate.Manifest;
        Files = Array.AsReadOnly(candidate.Files
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
            .ToArray());
    }

    /// <summary> Gets the canonical manifest. </summary>
    public SkillManifest Manifest { get; }

    /// <summary> Gets an immutable snapshot of the canonical package files. </summary>
    public IReadOnlyList<SkillPackageFile> Files { get; }

    /// <summary> Validates complete package candidates and creates canonical package snapshots. </summary>
    public sealed class Factory
    {
        private readonly SkillHostAdapterSet hostAdapters;
        private readonly SkillDigestCalculator digestCalculator;
        private readonly SkillManifestJsonSerializer manifestSerializer;

        /// <summary> Initializes the canonical package construction boundary. </summary>
        /// <param name="hostAdapters"> The complete supported host adapter set. </param>
        /// <param name="digestCalculator"> The canonical file digest calculator. </param>
        /// <param name="manifestSerializer"> The canonical manifest serializer. </param>
        public Factory (
            SkillHostAdapterSet hostAdapters,
            SkillDigestCalculator digestCalculator,
            SkillManifestJsonSerializer manifestSerializer)
        {
            this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
            this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
            this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
        }

        /// <summary> Validates one complete candidate and creates its canonical package snapshot. </summary>
        internal SkillOperationResult<CanonicalSkillPackage> CreateCanonical (CanonicalSkillPackageCandidate candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            var validationResult = Validate(candidate.Manifest, candidate.Files);
            return validationResult.IsSuccess
                ? SkillOperationResult<CanonicalSkillPackage>.Success(new CanonicalSkillPackage(candidate))
                : Failure(validationResult.Failure!.Message);
        }

        private SkillOperationResult<bool> Validate (
            SkillManifest manifest,
            IReadOnlyList<SkillPackageFile> files)
        {
            var portablePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                if (!portablePaths.Add(file.RelativePath))
                {
                    return BoolFailure($"Canonical SKILL package file paths must be unique when case is ignored: {file.RelativePath}");
                }
            }

            var filesByPath = files.ToDictionary(static file => file.RelativePath, StringComparer.Ordinal);
            if (!filesByPath.TryGetValue("agent-skill.json", out var manifestFile))
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

        private static SkillOperationResult<bool> ValidateFileSet (
            IReadOnlyDictionary<string, SkillPackageFile> filesByPath,
            SkillManifest manifest)
        {
            if (!filesByPath.ContainsKey("SKILL.md"))
            {
                return BoolFailure($"Generated SKILL package is missing SKILL.md: {manifest.SkillName}");
            }

            var hostArtifactPaths = manifest.HostArtifacts
                .Select(static artifact => artifact.Path)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var relativePath in filesByPath.Keys)
            {
                if (string.Equals(relativePath, "SKILL.md", StringComparison.Ordinal)
                    || string.Equals(relativePath, "agent-skill.json", StringComparison.Ordinal)
                    || relativePath.StartsWith("references/", StringComparison.Ordinal)
                    || hostArtifactPaths.Contains(relativePath))
                {
                    continue;
                }

                return BoolFailure($"Generated SKILL package contains an unsupported file: {manifest.SkillName}/{relativePath}");
            }

            return SkillOperationResult<bool>.Success(true);
        }

        private SkillOperationResult<bool> ValidateDigests (
            IReadOnlyDictionary<string, SkillPackageFile> filesByPath,
            SkillManifest manifest)
        {
            var contentDigest = digestCalculator.ComputeDigest(filesByPath.Values
                .Where(static file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)
                    || file.RelativePath.StartsWith("references/", StringComparison.Ordinal))
                .Select(static file => new SkillDigestInputFile(file.RelativePath, file.Content)));

            if (contentDigest != manifest.ContentDigest)
            {
                return BoolFailure($"Generated SKILL contentDigest does not match files: {manifest.SkillName}");
            }

            var metadata = new SkillHostMetadata(manifest.SkillName, manifest.DisplayName, manifest.Description);
            var artifactByHost = manifest.HostArtifacts.ToDictionary(static artifact => artifact.Host);
            foreach (var adapter in hostAdapters.Adapters)
            {
                var artifact = artifactByHost[adapter.Descriptor.Host];
                var hostArtifacts = adapter.BuildArtifacts(metadata);
                var metadataArtifactPath = adapter.Descriptor.MetadataArtifactPath;
                var frontmatterDigest = digestCalculator.ComputeSingleFileDigest("SKILL.md.frontmatter", hostArtifacts.Frontmatter);
                if (frontmatterDigest != artifact.MaterializedFrontmatterDigest)
                {
                    return BoolFailure($"Generated SKILL host frontmatter digest does not match adapter output: {manifest.SkillName}/{ContractLiteralCodec.ToValue(artifact.Host)}");
                }

                if (metadataArtifactPath is null)
                {
                    continue;
                }

                if (!filesByPath.TryGetValue(metadataArtifactPath, out var artifactFile))
                {
                    return BoolFailure($"Generated SKILL package is missing host artifact: {manifest.SkillName}/{metadataArtifactPath}");
                }

                if (hostArtifacts.MetadataContent is null)
                {
                    return BoolFailure($"Generated SKILL host artifact adapter output is missing: {manifest.SkillName}/{metadataArtifactPath}");
                }

                var expectedArtifactDigest = digestCalculator.ComputeSingleFileDigest(metadataArtifactPath, hostArtifacts.MetadataContent);
                if (expectedArtifactDigest != artifact.Digest)
                {
                    return BoolFailure($"Generated SKILL host artifact digest does not match adapter output: {manifest.SkillName}/{metadataArtifactPath}");
                }

                var artifactDigest = digestCalculator.ComputeSingleFileDigest(metadataArtifactPath, artifactFile.Content);
                if (artifactDigest != artifact.Digest)
                {
                    return BoolFailure($"Generated SKILL host artifact digest does not match files: {manifest.SkillName}/{metadataArtifactPath}");
                }
            }

            return SkillOperationResult<bool>.Success(true);
        }

        private static SkillOperationResult<CanonicalSkillPackage> Failure (string message)
        {
            return SkillOperationResult<CanonicalSkillPackage>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
        }

        private static SkillOperationResult<bool> BoolFailure (string message)
        {
            return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
        }
    }
}

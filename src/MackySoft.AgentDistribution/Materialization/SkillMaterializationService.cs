using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Packaging.Canonical;

namespace MackySoft.AgentDistribution.Materialization;

/// <summary> Validates canonical SKILL packages against current host adapters and materializes them for supported hosts. </summary>
public sealed class SkillMaterializationService
{
    private static readonly PackageRelativePath FrontmatterDigestPath = PackageRelativePath.Parse("SKILL.md.frontmatter");
    private static readonly PackageContentDigestCalculator DigestCalculator = new();

    /// <summary> Validates and materializes one canonical package for one host. </summary>
    /// <param name="package"> The canonical package. </param>
    /// <param name="host"> The target host. </param>
    /// <returns> The materialized package, or a failure when the host is unsupported or the package's host snapshot does not match the current adapter. </returns>
    /// <exception cref="ArgumentNullException"> <paramref name="package" /> is <see langword="null" />. </exception>
    /// <exception cref="InvalidOperationException"> The current host adapter emits metadata that contradicts its own descriptor. </exception>
    public AgentDistributionOperationResult<SkillMaterializedPackage> Materialize (
        CanonicalSkillPackage package,
        HostKind host)
    {
        ArgumentNullException.ThrowIfNull(package);

        var registrationResult = BuiltInHostCatalog.Get(host);
        if (!registrationResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillMaterializedPackage>.FailureResult(
                registrationResult.Failure!.Code,
                registrationResult.Failure.Message);
        }

        var metadata = new Hosts.Contracts.SkillHostMetadata(
            package.Manifest.SkillName,
            package.Manifest.DisplayName,
            package.Manifest.Description);

        var registration = registrationResult.Value!;
        var adapter = registration.SkillAdapter;
        var artifacts = adapter.BuildArtifacts(metadata);
        var metadataArtifactPath = registration.Skill.MetadataArtifactPath;
        var compatibilityResult = ValidateHostCompatibility(package, registration, artifacts);
        if (!compatibilityResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillMaterializedPackage>.FailureResult(
                compatibilityResult.Failure!.Code,
                compatibilityResult.Failure.Message);
        }

        var files = new List<PackageTextFile>();
        var hostArtifactFilePaths = package.Manifest.HostArtifacts
            .Select(static artifact => artifact.Path)
            .OfType<PackageRelativePath>()
            .ToHashSet();

        foreach (var file in package.Files)
        {
            if (file.RelativePath == PackageRelativePath.Parse("SKILL.md"))
            {
                files.Add(new PackageTextFile(file.RelativePath, artifacts.Frontmatter + "\n" + file.Content));
                continue;
            }

            if (hostArtifactFilePaths.Contains(file.RelativePath))
            {
                continue;
            }

            files.Add(file);
        }

        if (metadataArtifactPath is null)
        {
            if (artifacts.MetadataContent is not null)
            {
                throw new InvalidOperationException($"Host adapter '{Vocabulary.GetText(registration.Host)}' must not emit metadata artifacts.");
            }
        }
        else
        {
            if (artifacts.MetadataContent is null)
            {
                throw new InvalidOperationException($"Host adapter '{Vocabulary.GetText(registration.Host)}' must emit metadata artifact '{metadataArtifactPath}'.");
            }

            files.Add(new PackageTextFile(metadataArtifactPath, artifacts.MetadataContent));
        }

        return AgentDistributionOperationResult<SkillMaterializedPackage>.Success(new SkillMaterializedPackage(
            package.Manifest.SkillName,
            registration.Host,
            files.OrderBy(static file => file.RelativePath.Value, StringComparer.Ordinal).ToArray()));
    }

    private static AgentDistributionOperationResult<bool> ValidateHostCompatibility (
        CanonicalSkillPackage package,
        HostRegistration registration,
        SkillHostArtifactSet artifacts)
    {
        var manifestArtifact = package.Manifest.HostArtifacts.Single(artifact => artifact.Host == registration.Host);
        var frontmatterDigest = DigestCalculator.ComputeSingleFileDigest(FrontmatterDigestPath, artifacts.Frontmatter);
        if (frontmatterDigest != manifestArtifact.MaterializedFrontmatterDigest)
        {
            return CompatibilityFailure($"Generated SKILL host frontmatter digest does not match adapter output: {package.Manifest.SkillName}/{Vocabulary.GetText(registration.Host)}");
        }

        var metadataArtifactPath = registration.Skill.MetadataArtifactPath;
        if (manifestArtifact.Path != metadataArtifactPath)
        {
            return CompatibilityFailure($"Generated SKILL host artifact path does not match the current adapter: {package.Manifest.SkillName}/{Vocabulary.GetText(registration.Host)}");
        }

        if (metadataArtifactPath is null)
        {
            return AgentDistributionOperationResult<bool>.Success(true);
        }

        if (artifacts.MetadataContent is null)
        {
            return CompatibilityFailure($"Generated SKILL host artifact adapter output is missing: {package.Manifest.SkillName}/{metadataArtifactPath.Value}");
        }

        var metadataDigest = DigestCalculator.ComputeSingleFileDigest(metadataArtifactPath, artifacts.MetadataContent);
        return metadataDigest == manifestArtifact.Digest
            ? AgentDistributionOperationResult<bool>.Success(true)
            : CompatibilityFailure($"Generated SKILL host artifact digest does not match adapter output: {package.Manifest.SkillName}/{metadataArtifactPath.Value}");
    }

    private static AgentDistributionOperationResult<bool> CompatibilityFailure (string message)
    {
        return AgentDistributionOperationResult<bool>.FailureResult(AgentDistributionFailureCodes.ManifestInvalid, message);
    }
}

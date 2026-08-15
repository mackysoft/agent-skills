using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Manifests;
using MackySoft.AgentDistribution.Skills.Packaging.Canonical;

namespace MackySoft.AgentDistribution.Installation.Validation;

/// <summary> Builds the managed package-relative file set used by target validation. </summary>
internal static class SkillManagedFileSetPaths
{
    /// <summary> Gets the package-relative path for the installed SKILL body. </summary>
    public static PackageRelativePath SkillBodyPath { get; } = SkillContentFileSetPaths.SkillBodyPath;

    /// <summary> Gets the package-relative path for the installed Agent Distribution manifest. </summary>
    public static PackageRelativePath ManifestPath { get; } = PackageRelativePath.Parse("agent-skill.json");

    /// <summary> Gets the package-relative path prefix for host-independent reference files. </summary>
    public static PackageRelativePath ReferencesDirectoryPath { get; } = SkillContentFileSetPaths.ReferencesDirectoryPath;

    /// <summary> Gets the package-relative path prefix for host-independent script files. </summary>
    public static PackageRelativePath ScriptsDirectoryPath { get; } = SkillContentFileSetPaths.ScriptsDirectoryPath;

    /// <summary> Creates the managed file set expected from a materialized canonical package for one host. </summary>
    /// <param name="package"> The canonical package to inspect. Must not be <see langword="null" />. </param>
    /// <param name="host"> The requested host. </param>
    /// <returns> A success result containing package-relative paths from <paramref name="package" /> that are managed for <paramref name="host" />; otherwise a failure when the manifest has no artifact for <paramref name="host" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="package" /> is <see langword="null" />. </exception>
    public static AgentDistributionOperationResult<IReadOnlyCollection<PackageRelativePath>> CreateMaterializedRequiredPaths (
        CanonicalSkillPackage package,
        HostKind host)
    {
        ArgumentNullException.ThrowIfNull(package);

        var hostArtifactResult = GetHostArtifact(package.Manifest, host);
        if (!hostArtifactResult.IsSuccess)
        {
            return AgentDistributionOperationResult<IReadOnlyCollection<PackageRelativePath>>.FailureResult(
                hostArtifactResult.Failure!.Code,
                hostArtifactResult.Failure.Message);
        }

        var hostArtifactPath = hostArtifactResult.Value!.Path;
        var paths = package.Files
            .Select(static file => file.RelativePath)
            .Where(path => path.Equals(ManifestPath)
                || SkillContentFileSetPaths.IsContentFile(path)
                || (hostArtifactPath is not null && path.Equals(hostArtifactPath)))
            .ToArray();

        return AgentDistributionOperationResult<IReadOnlyCollection<PackageRelativePath>>.Success(paths);
    }

    /// <summary> Creates the managed file set that can be derived from an installed manifest for one host. </summary>
    /// <param name="manifest"> The installed manifest to inspect. Must not be <see langword="null" />. </param>
    /// <param name="host"> The requested host. </param>
    /// <returns> A success result containing the manifest, SKILL body, and requested host artifact paths; otherwise a failure when <paramref name="manifest" /> has no artifact for <paramref name="host" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="manifest" /> is <see langword="null" />. </exception>
    public static AgentDistributionOperationResult<IReadOnlyCollection<PackageRelativePath>> CreateInstalledManifestRequiredPaths (
        SkillManifest manifest,
        HostKind host)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var hostArtifactResult = GetHostArtifact(manifest, host);
        if (!hostArtifactResult.IsSuccess)
        {
            return AgentDistributionOperationResult<IReadOnlyCollection<PackageRelativePath>>.FailureResult(
                hostArtifactResult.Failure!.Code,
                hostArtifactResult.Failure.Message);
        }

        var paths = new HashSet<PackageRelativePath>()
        {
            SkillBodyPath,
            ManifestPath,
        };

        var hostArtifactPath = hostArtifactResult.Value!.Path;
        if (hostArtifactPath is not null)
        {
            paths.Add(hostArtifactPath);
        }

        return AgentDistributionOperationResult<IReadOnlyCollection<PackageRelativePath>>.Success(paths);
    }

    private static AgentDistributionOperationResult<SkillHostArtifactManifest> GetHostArtifact (
        SkillManifest manifest,
        HostKind host)
    {
        var hostArtifact = manifest.HostArtifacts.SingleOrDefault(artifact => artifact.Host == host);
        return hostArtifact is null
            ? AgentDistributionOperationResult<SkillHostArtifactManifest>.FailureResult(
                AgentDistributionFailureCodes.ManifestInvalid,
                $"Manifest does not contain host artifact '{Vocabulary.GetText(host)}'.")
            : AgentDistributionOperationResult<SkillHostArtifactManifest>.Success(hostArtifact);
    }
}

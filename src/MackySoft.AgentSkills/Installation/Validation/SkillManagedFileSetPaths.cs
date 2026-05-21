using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Validation;

/// <summary> Builds the managed package-relative file set used by target validation. </summary>
internal static class SkillManagedFileSetPaths
{
    /// <summary> Gets the package-relative path for the installed SKILL body. </summary>
    public const string SkillBodyPath = "SKILL.md";

    /// <summary> Gets the package-relative path for the installed Agent Skills manifest. </summary>
    public const string ManifestPath = "agent-skill.json";

    /// <summary> Gets the package-relative path prefix for host-independent reference files. </summary>
    public const string ReferencesPrefix = "references/";

    /// <summary> Creates the managed file set expected from a materialized canonical package for one host. </summary>
    /// <param name="package"> The canonical package to inspect. Must not be <see langword="null" />. </param>
    /// <param name="host"> The requested host key. Must not be null, empty, or whitespace. </param>
    /// <returns> A success result containing package-relative paths from <paramref name="package" /> that are managed for <paramref name="host" />; otherwise a failure when the manifest has no artifact for <paramref name="host" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="package" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="host" /> is null, empty, or whitespace. </exception>
    public static SkillOperationResult<IReadOnlyCollection<string>> CreateMaterializedRequiredPaths (
        CanonicalSkillPackage package,
        string host)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        var hostArtifactResult = GetHostArtifact(package.Manifest, host);
        if (!hostArtifactResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyCollection<string>>.FailureResult(
                hostArtifactResult.Failure!.Code,
                hostArtifactResult.Failure.Message);
        }

        var hostArtifactPath = hostArtifactResult.Value!.Path;
        var paths = package.Files
            .Select(static file => file.RelativePath)
            .Where(path => string.Equals(path, ManifestPath, StringComparison.Ordinal)
                || string.Equals(path, SkillBodyPath, StringComparison.Ordinal)
                || path.StartsWith(ReferencesPrefix, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(hostArtifactPath) && string.Equals(path, hostArtifactPath, StringComparison.Ordinal)))
            .ToArray();

        return SkillOperationResult<IReadOnlyCollection<string>>.Success(paths);
    }

    /// <summary> Creates the managed file set that can be derived from an installed manifest for one host. </summary>
    /// <param name="manifest"> The installed manifest to inspect. Must not be <see langword="null" />. </param>
    /// <param name="host"> The requested host key. Must not be null, empty, or whitespace. </param>
    /// <returns> A success result containing the manifest, SKILL body, and requested host artifact paths; otherwise a failure when <paramref name="manifest" /> has no artifact for <paramref name="host" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="manifest" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="host" /> is null, empty, or whitespace. </exception>
    public static SkillOperationResult<IReadOnlyCollection<string>> CreateInstalledManifestRequiredPaths (
        SkillManifest manifest,
        string host)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        var hostArtifactResult = GetHostArtifact(manifest, host);
        if (!hostArtifactResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyCollection<string>>.FailureResult(
                hostArtifactResult.Failure!.Code,
                hostArtifactResult.Failure.Message);
        }

        var paths = new HashSet<string>(StringComparer.Ordinal)
        {
            SkillBodyPath,
            ManifestPath,
        };

        var hostArtifactPath = hostArtifactResult.Value!.Path;
        if (!string.IsNullOrWhiteSpace(hostArtifactPath))
        {
            paths.Add(hostArtifactPath);
        }

        return SkillOperationResult<IReadOnlyCollection<string>>.Success(paths);
    }

    private static SkillOperationResult<SkillHostArtifactManifest> GetHostArtifact (
        SkillManifest manifest,
        string host)
    {
        var hostArtifact = manifest.HostArtifacts.SingleOrDefault(artifact => string.Equals(artifact.Host, host, StringComparison.Ordinal));
        return hostArtifact is null
            ? SkillOperationResult<SkillHostArtifactManifest>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"Manifest does not contain host artifact '{host}'.")
            : SkillOperationResult<SkillHostArtifactManifest>.Success(hostArtifact);
    }
}

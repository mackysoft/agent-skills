using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Validation;

internal static class SkillManagedFileSetPaths
{
    public const string SkillBodyPath = "SKILL.md";
    public const string ManifestPath = "agent-skill.json";
    public const string ReferencesPrefix = "references/";

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

using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Packaging;

/// <summary> Owns the canonical package layout for one generated Agent host artifact. </summary>
internal static class AgentHostArtifactPackagePath
{
    /// <summary> Creates a canonical package path from one host-relative artifact path. </summary>
    public static PackageRelativePath Create (HostKind hostId, PackageRelativePath hostRelativePath)
    {
        ArgumentNullException.ThrowIfNull(hostRelativePath);
        return PackageRelativePath.Parse($"{GetHostDirectory(hostId).Value}/{hostRelativePath.Value}");
    }

    /// <summary> Derives the host-target-root-relative path from a canonical package artifact path. </summary>
    public static PackageRelativePath GetHostRelativePath (HostKind hostId, PackageRelativePath packageArtifactPath)
    {
        ArgumentNullException.ThrowIfNull(packageArtifactPath);
        var hostDirectory = GetHostDirectory(hostId);
        if (!packageArtifactPath.TryGetRelativeTo(hostDirectory, out var hostRelativePath))
        {
            throw new ArgumentException(
                $"Agent host artifact path must be below '{hostDirectory}': {packageArtifactPath.Value}",
                nameof(packageArtifactPath));
        }

        return hostRelativePath;
    }

    private static PackageRelativePath GetHostDirectory (HostKind hostId)
    {
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }

        return PackageRelativePath.Parse($"hosts/{Vocabulary.GetText(hostId)}");
    }
}

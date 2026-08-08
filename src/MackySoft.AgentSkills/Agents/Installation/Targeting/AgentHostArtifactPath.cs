using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Installation.Targeting;

/// <summary> Converts canonical agent package host-artifact paths to host target-root-relative paths. </summary>
internal static class AgentHostArtifactPath
{
    /// <summary> Removes the canonical host package prefix from one artifact path. </summary>
    public static SkillOperationResult<PackageRelativePath> ResolveTargetRelativePath (PackageRelativePath packageArtifactPath, HostKind hostId)
    {
        ArgumentNullException.ThrowIfNull(packageArtifactPath);
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }

        var hostLiteral = Vocabulary.GetText(hostId);
        var hostDirectoryPath = PackageRelativePath.Parse($"hosts/{hostLiteral}");
        if (!packageArtifactPath.TryGetRelativeTo(hostDirectoryPath, out var targetRelativePath))
        {
            return SkillOperationResult<PackageRelativePath>.FailureResult(SkillFailureCodes.ManifestInvalid, $"Agent host artifact path does not belong to '{hostLiteral}': {packageArtifactPath.Value}");
        }

        return SkillOperationResult<PackageRelativePath>.Success(targetRelativePath);
    }
}

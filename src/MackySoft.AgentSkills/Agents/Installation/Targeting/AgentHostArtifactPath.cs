using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Installation.Targeting;

/// <summary> Converts canonical agent package host-artifact paths to host target-root-relative paths. </summary>
internal static class AgentHostArtifactPath
{
    /// <summary> Removes the canonical host package prefix from one artifact path. </summary>
    public static SkillOperationResult<string> ResolveTargetRelativePath (string packageArtifactPath, AgentHostKind hostId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageArtifactPath);
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }

        var hostLiteral = Vocabulary.GetText(hostId);
        var prefix = $"hosts/{hostLiteral}/";
        if (!packageArtifactPath.StartsWith(prefix, StringComparison.Ordinal))
        {
            return SkillOperationResult<string>.FailureResult(SkillFailureCodes.ManifestInvalid, $"Agent host artifact path does not belong to '{hostLiteral}': {packageArtifactPath}");
        }

        var relativePath = packageArtifactPath[prefix.Length..];
        return PackageRelativePath.TryParse(relativePath, out var targetRelativePath)
            ? SkillOperationResult<string>.Success(targetRelativePath.Value)
            : SkillOperationResult<string>.FailureResult(SkillFailureCodes.ManifestInvalid, $"Agent host artifact path is unsafe: {packageArtifactPath}");
    }
}

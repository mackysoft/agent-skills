using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Installation.Targeting;

/// <summary> Resolves agent paths without allowing root escapes or existing reparse points. </summary>
internal static class AgentPathGuard
{
    /// <summary> Resolves one path that must remain inside a regular root directory. </summary>
    public static SkillOperationResult<string> ResolveUnderRoot (string rootPath, string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        try
        {
            if (!AbsolutePath.TryParse(rootPath, out var root, out var rootFailure))
            {
                return Failure($"Agent path root is invalid: {rootFailure.Message}");
            }

            if (!ContainedPath.TryResolve(root, targetPath, out var containedPath, out var targetFailure))
            {
                return Failure($"Agent path is invalid: {targetFailure.Message}");
            }

            return ValidateContainedPath(containedPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return Failure($"Could not resolve agent path. {exception.Message}");
        }
    }

    /// <summary> Validates the physical segments of an already established lexical containment relationship. </summary>
    internal static SkillOperationResult<string> ValidateContainedPath (ContainedPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            var reparseResult = RejectExistingReparsePoints(path);
            return reparseResult.IsSuccess
                ? SkillOperationResult<string>.Success(path.Target.Value)
                : SkillOperationResult<string>.FailureResult(reparseResult.Failure!.Code, reparseResult.Failure.Message);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return Failure($"Could not inspect agent path. {exception.Message}");
        }
    }

    /// <summary> Resolves one standalone target while rejecting a reparse-point target itself. </summary>
    public static SkillOperationResult<string> ResolveStandaloneRoot (string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        return ResolveUnderRoot(targetPath, targetPath);
    }

    /// <summary> Resolves one managed artifact path below an agent artifact root. </summary>
    public static SkillOperationResult<string> ResolveArtifactPath (string artifactRoot, string relativePath)
    {
        if (!PackageRelativePath.TryParse(relativePath, out var packageRelativePath))
        {
            return Failure($"Managed agent artifact path is unsafe: {relativePath}");
        }

        if (!AbsolutePath.TryParse(artifactRoot, out var absoluteArtifactRoot, out var rootFailure))
        {
            return Failure($"Agent artifact root is invalid: {rootFailure.Message}");
        }

        return ValidateContainedPath(ContainedPath.Create(absoluteArtifactRoot, packageRelativePath.RootRelativePath));
    }

    private static SkillOperationResult<bool> RejectExistingReparsePoints (ContainedPath path)
    {
        var current = path.BoundaryRoot.Value;
        if (Directory.Exists(current) && IsReparsePoint(current))
        {
            return Failure<bool>("Agent path root must not be a reparse point.");
        }

        if (path.RelativePath.IsRoot)
        {
            return SkillOperationResult<bool>.Success(true);
        }

        foreach (var segment in path.RelativePath.Value.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if ((Directory.Exists(current) || File.Exists(current)) && IsReparsePoint(current))
            {
                return Failure<bool>($"Agent path must not traverse a reparse point: {current}");
            }
        }

        return SkillOperationResult<bool>.Success(true);
    }

    private static bool IsReparsePoint (string path)
    {
        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    private static SkillOperationResult<T> Failure<T> (string message)
    {
        return SkillOperationResult<T>.FailureResult(SkillFailureCodes.PathUnsafe, message);
    }

    private static SkillOperationResult<string> Failure (string message)
    {
        return Failure<string>(message);
    }
}

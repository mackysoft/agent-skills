using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Installation.Targeting;

/// <summary> Resolves agent paths without allowing root escapes or existing links and reparse points. </summary>
internal static class AgentPathGuard
{
    /// <summary> Validates the physical segments of an already established lexical containment relationship. </summary>
    internal static SkillOperationResult<AbsolutePath> Validate (ContainedPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            if (!PhysicalPathResolver.TryResolve(
                    path,
                    SymbolicLinkHandling.Reject,
                    MissingPathHandling.AllowMissingTail,
                    out var resolution,
                    out var failure))
            {
                return Failure($"Could not resolve agent path. {failure.Message}");
            }

            // NOTE: The physical resolution is a snapshot. Agent contracts retain their guarded lexical path.
            return SkillOperationResult<AbsolutePath>.Success(resolution.RequestedPath.Target);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return Failure($"Could not inspect agent path. {exception.Message}");
        }
    }

    private static SkillOperationResult<T> Failure<T> (string message)
    {
        return SkillOperationResult<T>.FailureResult(SkillFailureCodes.PathUnsafe, message);
    }

    private static SkillOperationResult<AbsolutePath> Failure (string message)
    {
        return Failure<AbsolutePath>(message);
    }
}

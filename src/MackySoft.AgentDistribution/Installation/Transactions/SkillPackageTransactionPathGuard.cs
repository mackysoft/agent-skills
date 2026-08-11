using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Transactions;

/// <summary> Validates transaction directories before file-system writes or cleanup. </summary>
internal static class SkillPackageTransactionPathGuard
{
    /// <summary> Verifies that a created transaction directory is a regular directory under the bundle target root. </summary>
    /// <param name="targetRoot"> The resolved bundle target root. </param>
    /// <param name="directoryPath"> The transaction directory path. </param>
    /// <returns> Success when the directory is not a link and resolves under the bundle target root. </returns>
    public static AgentDistributionOperationResult<bool> ValidateCreatedDirectory (
        AbsolutePath targetRoot,
        AbsolutePath directoryPath)
    {
        ArgumentNullException.ThrowIfNull(targetRoot);
        ArgumentNullException.ThrowIfNull(directoryPath);

        if (!ContainedPath.TryCreate(targetRoot, directoryPath, out var containedPath, out var pathFailure))
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"SKILL package transaction directory is unsafe: {pathFailure.Message}");
        }

        if (!PhysicalPathResolver.TryResolve(
                containedPath,
                SymbolicLinkHandling.Reject,
                MissingPathHandling.Reject,
                out var resolution,
                out var physicalFailure))
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                physicalFailure.Kind == FileSystemOperationFailureKind.EntryNotFound
                    ? AgentDistributionFailureCodes.InstallTargetWriteFailed
                    : AgentDistributionFailureCodes.PathUnsafe,
                $"SKILL package transaction directory is invalid: {physicalFailure.Message}");
        }

        if (resolution.TargetObservation.State != FileSystemEntryState.Directory)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"SKILL package transaction directory must be a regular directory: {directoryPath.Value}");
        }

        return AgentDistributionOperationResult<bool>.Success(true);
    }
}

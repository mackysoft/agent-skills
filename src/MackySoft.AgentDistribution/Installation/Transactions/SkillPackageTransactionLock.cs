using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Transactions;

/// <summary> Acquires cooperative locks for SKILL package transaction directories. </summary>
internal static class SkillPackageTransactionLock
{
    /// <summary> Acquires an exclusive lock file under the transaction directory. </summary>
    /// <param name="targetRoot"> The resolved bundle target root. </param>
    /// <param name="transactionRoot"> The transaction directory under the target root. </param>
    /// <returns> A disposable lock handle or a write failure. </returns>
    public static SkillOperationResult<IDisposable> Acquire (
        AbsolutePath targetRoot,
        AbsolutePath transactionRoot)
    {
        var lockPathResult = PackagePathResolver.ResolveUnderRoot(
            targetRoot,
            ContainedPath.Create(transactionRoot, RootRelativePath.Parse(".lock")).Target);
        if (!lockPathResult.IsSuccess)
        {
            return SkillOperationResult<IDisposable>.FailureResult(lockPathResult.Failure!.Code, lockPathResult.Failure.Message);
        }

        try
        {
            return SkillOperationResult<IDisposable>.Success(new FileStream(
                lockPathResult.Value!.Value,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SkillOperationResult<IDisposable>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"Failed to acquire SKILL package transaction lock: {lockPathResult.Value}. {ex.Message}");
        }
    }
}

using MackySoft.AgentDistribution.Installation.Contracts;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Transactions;

/// <summary> Deletes installed SKILL package directories under a resolved bundle target root. </summary>
public sealed class SkillInstalledPackageRemover : ISkillInstalledPackageRemover
{
    private readonly ISkillPackageDirectoryOperations directoryOperations;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledPackageRemover" /> class. </summary>
    /// <param name="directoryOperations"> The directory operations used by package transactions. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="directoryOperations" /> is <see langword="null" />. </exception>
    public SkillInstalledPackageRemover (ISkillPackageDirectoryOperations directoryOperations)
    {
        this.directoryOperations = directoryOperations ?? throw new ArgumentNullException(nameof(directoryOperations));
    }

    /// <inheritdoc />
    public async ValueTask<AgentDistributionOperationResult<bool>> DeleteAsync (
        AbsolutePath targetRoot,
        AbsolutePath skillDirectory,
        Func<AbsolutePath, CancellationToken, ValueTask<AgentDistributionOperationResult<bool>>>? precondition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetRoot);
        ArgumentNullException.ThrowIfNull(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var targetRootResult = PackagePathResolver.ResolveUnderRoot(targetRoot, targetRoot);
        if (!targetRootResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                targetRootResult.Failure!.Code,
                targetRootResult.Failure.Message);
        }

        var resolvedTargetRoot = targetRootResult.Value!;
        var skillDirectoryResult = PackagePathResolver.ResolveUnderRoot(resolvedTargetRoot, skillDirectory);
        if (!skillDirectoryResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                skillDirectoryResult.Failure!.Code,
                skillDirectoryResult.Failure.Message);
        }

        var resolvedSkillDirectory = skillDirectoryResult.Value!;
        if (resolvedTargetRoot.IsSameAs(resolvedSkillDirectory))
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"Skill directory must not be the target root: {resolvedSkillDirectory}");
        }

        if (!directoryOperations.Exists(resolvedSkillDirectory))
        {
            if (precondition is not null)
            {
                var preconditionResult = await precondition(resolvedSkillDirectory, cancellationToken).ConfigureAwait(false);
                if (!preconditionResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<bool>.FailureResult(preconditionResult.Failure!.Code, preconditionResult.Failure.Message);
                }

                return AgentDistributionOperationResult<bool>.FailureResult(
                    AgentDistributionFailureCodes.InstallTargetDigestMismatch,
                    $"Target skill directory changed after planning; refusing to delete: {resolvedSkillDirectory}");
            }

            return AgentDistributionOperationResult<bool>.Success(true);
        }

        if (!resolvedSkillDirectory.TryGetParent(out var parentDirectory))
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetWriteFailed,
                $"Skill directory parent could not be resolved: {resolvedSkillDirectory}");
        }

        var transactionRootResult = PackagePathResolver.ResolveUnderRoot(
            resolvedTargetRoot,
            ContainedPath.Create(parentDirectory, RootRelativePath.Parse(".agent-distribution-skill-transactions")).Target);
        if (!transactionRootResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                transactionRootResult.Failure!.Code,
                transactionRootResult.Failure.Message);
        }

        var deletedContainerResult = PackagePathResolver.ResolveUnderRoot(
            resolvedTargetRoot,
            ContainedPath.Create(
                transactionRootResult.Value!,
                RootRelativePath.Parse($"{Path.GetFileName(resolvedSkillDirectory.Value)}.delete.{Guid.NewGuid():N}")).Target);
        if (!deletedContainerResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                deletedContainerResult.Failure!.Code,
                deletedContainerResult.Failure.Message);
        }

        var deletedDirectoryResult = PackagePathResolver.ResolveUnderRoot(
            resolvedTargetRoot,
            ContainedPath.Create(
                deletedContainerResult.Value!,
                RootRelativePath.Parse(Path.GetFileName(resolvedSkillDirectory.Value))).Target);
        if (!deletedDirectoryResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                deletedDirectoryResult.Failure!.Code,
                deletedDirectoryResult.Failure.Message);
        }

        var transactionRoot = transactionRootResult.Value!;
        var deletedContainer = deletedContainerResult.Value!;
        var deletedDirectory = deletedDirectoryResult.Value!;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            directoryOperations.Create(transactionRoot);
            var transactionRootGuard = SkillPackageTransactionPathGuard.ValidateCreatedDirectory(resolvedTargetRoot, transactionRoot);
            if (!transactionRootGuard.IsSuccess)
            {
                return AgentDistributionOperationResult<bool>.FailureResult(
                    transactionRootGuard.Failure!.Code,
                    transactionRootGuard.Failure.Message);
            }

            var lockResult = SkillPackageTransactionLock.Acquire(resolvedTargetRoot, transactionRoot);
            if (!lockResult.IsSuccess)
            {
                return AgentDistributionOperationResult<bool>.FailureResult(lockResult.Failure!.Code, lockResult.Failure.Message);
            }

            using var transactionLock = lockResult.Value!;
            if (precondition is not null)
            {
                var preconditionResult = await precondition(resolvedSkillDirectory, cancellationToken).ConfigureAwait(false);
                if (!preconditionResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<bool>.FailureResult(preconditionResult.Failure!.Code, preconditionResult.Failure.Message);
                }
            }

            if (!directoryOperations.Exists(resolvedSkillDirectory))
            {
                return AgentDistributionOperationResult<bool>.FailureResult(
                    AgentDistributionFailureCodes.InstallTargetDigestMismatch,
                    $"Target skill directory changed after planning; refusing to delete: {resolvedSkillDirectory}");
            }

            directoryOperations.Create(deletedContainer);
            var deletedContainerGuard = SkillPackageTransactionPathGuard.ValidateCreatedDirectory(resolvedTargetRoot, deletedContainer);
            if (!deletedContainerGuard.IsSuccess)
            {
                return AgentDistributionOperationResult<bool>.FailureResult(deletedContainerGuard.Failure!.Code, deletedContainerGuard.Failure.Message);
            }

            directoryOperations.Move(resolvedSkillDirectory, deletedDirectory);
            if (precondition is not null)
            {
                var movedTargetResult = await precondition(deletedDirectory, cancellationToken).ConfigureAwait(false);
                if (!movedTargetResult.IsSuccess)
                {
                    directoryOperations.Move(deletedDirectory, resolvedSkillDirectory);
                    return AgentDistributionOperationResult<bool>.FailureResult(movedTargetResult.Failure!.Code, movedTargetResult.Failure.Message);
                }
            }

            DeleteDirectoryBestEffort(deletedDirectory);
            DeleteDirectoryBestEffort(transactionRoot);

            return AgentDistributionOperationResult<bool>.Success(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetWriteFailed,
                $"Failed to delete installed SKILL package: {resolvedSkillDirectory}. {ex.Message}");
        }
        finally
        {
            if (!directoryOperations.Exists(deletedDirectory))
            {
                DeleteDirectoryBestEffort(transactionRoot);
            }
        }
    }

    private void DeleteDirectoryBestEffort (AbsolutePath path)
    {
        if (!directoryOperations.Exists(path))
        {
            return;
        }

        try
        {
            directoryOperations.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

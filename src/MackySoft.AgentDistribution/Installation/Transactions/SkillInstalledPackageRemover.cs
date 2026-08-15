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

        var transactionLockRootResult = PackagePathResolver.ResolveUnderRoot(
            resolvedTargetRoot,
            ContainedPath.Create(parentDirectory, RootRelativePath.Parse(".agent-distribution-skill-transactions")).Target);
        if (!transactionLockRootResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                transactionLockRootResult.Failure!.Code,
                transactionLockRootResult.Failure.Message);
        }

        var workspaceResult = PackagePathResolver.ResolveUnderRoot(
            resolvedTargetRoot,
            ContainedPath.Create(
                transactionLockRootResult.Value!,
                RootRelativePath.Parse(Guid.NewGuid().ToString("N"))).Target);
        if (!workspaceResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                workspaceResult.Failure!.Code,
                workspaceResult.Failure.Message);
        }

        var backupDirectoryResult = PackagePathResolver.ResolveUnderRoot(
            resolvedTargetRoot,
            ContainedPath.Create(
                workspaceResult.Value!,
                RootRelativePath.Parse(Path.GetFileName(resolvedSkillDirectory.Value))).Target);
        if (!backupDirectoryResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                backupDirectoryResult.Failure!.Code,
                backupDirectoryResult.Failure.Message);
        }

        var transaction = new SkillPackageDeleteTransaction(
            resolvedTargetRoot,
            resolvedSkillDirectory,
            precondition,
            transactionLockRootResult.Value!,
            workspaceResult.Value!,
            backupDirectoryResult.Value!);

        return await ExecuteTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> ExecuteTransactionAsync (
        SkillPackageDeleteTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteWithinTransactionAsync(transaction, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var restoreResult = RestoreMovedTargetIfNeeded(transaction);
            if (!restoreResult.IsSuccess)
            {
                return restoreResult;
            }

            throw;
        }
        catch (Exception ex)
        {
            var restoreResult = RestoreMovedTargetIfNeeded(transaction);
            if (!restoreResult.IsSuccess)
            {
                return restoreResult;
            }

            if (ex is IOException or UnauthorizedAccessException)
            {
                return AgentDistributionOperationResult<bool>.FailureResult(
                    AgentDistributionFailureCodes.InstallTargetWriteFailed,
                    $"Failed to delete installed SKILL package: {transaction.SkillDirectory}. {ex.Message}");
            }

            throw;
        }
        finally
        {
            CleanupTransaction(transaction);
        }
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> ExecuteWithinTransactionAsync (
        SkillPackageDeleteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var lockRootResult = CreateVerifiedDirectory(transaction.TargetRoot, transaction.TransactionLockRoot);
        if (!lockRootResult.IsSuccess)
        {
            return lockRootResult;
        }

        var lockResult = SkillPackageTransactionLock.Acquire(transaction.TargetRoot, transaction.TransactionLockRoot);
        if (!lockResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(lockResult.Failure!.Code, lockResult.Failure.Message);
        }

        using var transactionLock = lockResult.Value!;
        return await DeleteLockedAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> DeleteLockedAsync (
        SkillPackageDeleteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var preconditionResult = await InvokePreconditionAsync(transaction, transaction.SkillDirectory, cancellationToken).ConfigureAwait(false);
        if (!preconditionResult.IsSuccess)
        {
            return preconditionResult;
        }

        if (!directoryOperations.Exists(transaction.SkillDirectory))
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetDigestMismatch,
                $"Target skill directory changed after planning; refusing to delete: {transaction.SkillDirectory}");
        }

        var workspaceResult = CreateVerifiedDirectory(transaction.TargetRoot, transaction.WorkspaceDirectory);
        if (!workspaceResult.IsSuccess)
        {
            return workspaceResult;
        }

        directoryOperations.Move(transaction.SkillDirectory, transaction.BackupDirectory);
        transaction.MarkTargetMovedToBackup();

        var movedTargetResult = await InvokePreconditionAsync(transaction, transaction.BackupDirectory, cancellationToken).ConfigureAwait(false);
        if (!movedTargetResult.IsSuccess)
        {
            return RestoreTargetBeforeReturningFailure(transaction, movedTargetResult);
        }

        cancellationToken.ThrowIfCancellationRequested();
        DeleteDirectoryBestEffort(transaction.BackupDirectory);
        transaction.MarkDeletionCommitted();
        return AgentDistributionOperationResult<bool>.Success(true);
    }

    private static async ValueTask<AgentDistributionOperationResult<bool>> InvokePreconditionAsync (
        SkillPackageDeleteTransaction transaction,
        AbsolutePath path,
        CancellationToken cancellationToken)
    {
        return transaction.Precondition is null
            ? AgentDistributionOperationResult<bool>.Success(true)
            : await transaction.Precondition(path, cancellationToken).ConfigureAwait(false);
    }

    private AgentDistributionOperationResult<bool> RestoreTargetBeforeReturningFailure (
        SkillPackageDeleteTransaction transaction,
        AgentDistributionOperationResult<bool> failureResult)
    {
        var restoreResult = RestoreMovedTargetIfNeeded(transaction);
        return restoreResult.IsSuccess ? failureResult : restoreResult;
    }

    private AgentDistributionOperationResult<bool> RestoreMovedTargetIfNeeded (SkillPackageDeleteTransaction transaction)
    {
        if (!transaction.TargetMovedToBackup
            || transaction.DeletionCommitted
            || directoryOperations.Exists(transaction.SkillDirectory)
            || !directoryOperations.Exists(transaction.BackupDirectory))
        {
            return AgentDistributionOperationResult<bool>.Success(true);
        }

        try
        {
            directoryOperations.Move(transaction.BackupDirectory, transaction.SkillDirectory);
            transaction.MarkTargetRestored();
            return AgentDistributionOperationResult<bool>.Success(true);
        }
        catch (Exception restoreException) when (restoreException is IOException or UnauthorizedAccessException)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetWriteFailed,
                $"Failed to delete installed SKILL package and restore backup: {transaction.SkillDirectory}. Backup remains at: {transaction.BackupDirectory}. {restoreException.Message}");
        }
    }

    private AgentDistributionOperationResult<bool> CreateVerifiedDirectory (
        AbsolutePath targetRoot,
        AbsolutePath directory)
    {
        directoryOperations.Create(directory);
        return SkillPackageTransactionPathGuard.ValidateCreatedDirectory(targetRoot, directory);
    }

    private void CleanupTransaction (SkillPackageDeleteTransaction transaction)
    {
        if (!directoryOperations.Exists(transaction.BackupDirectory))
        {
            DeleteDirectoryBestEffort(transaction.WorkspaceDirectory);
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

    private sealed class SkillPackageDeleteTransaction
    {
        public SkillPackageDeleteTransaction (
            AbsolutePath targetRoot,
            AbsolutePath skillDirectory,
            Func<AbsolutePath, CancellationToken, ValueTask<AgentDistributionOperationResult<bool>>>? precondition,
            AbsolutePath transactionLockRoot,
            AbsolutePath workspaceDirectory,
            AbsolutePath backupDirectory)
        {
            TargetRoot = targetRoot;
            SkillDirectory = skillDirectory;
            Precondition = precondition;
            TransactionLockRoot = transactionLockRoot;
            WorkspaceDirectory = workspaceDirectory;
            BackupDirectory = backupDirectory;
        }

        public AbsolutePath TargetRoot { get; }

        public AbsolutePath SkillDirectory { get; }

        public Func<AbsolutePath, CancellationToken, ValueTask<AgentDistributionOperationResult<bool>>>? Precondition { get; }

        public AbsolutePath TransactionLockRoot { get; }

        public AbsolutePath WorkspaceDirectory { get; }

        public AbsolutePath BackupDirectory { get; }

        public bool TargetMovedToBackup { get; private set; }

        public bool DeletionCommitted { get; private set; }

        public void MarkTargetMovedToBackup ()
        {
            TargetMovedToBackup = true;
        }

        public void MarkTargetRestored ()
        {
            TargetMovedToBackup = false;
        }

        public void MarkDeletionCommitted ()
        {
            DeletionCommitted = true;
        }
    }
}

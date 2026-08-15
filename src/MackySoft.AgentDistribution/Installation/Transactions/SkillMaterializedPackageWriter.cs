using MackySoft.AgentDistribution.Installation.Contracts;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Serialization;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Transactions;

/// <summary> Writes materialized SKILL packages under a resolved bundle target root. </summary>
public sealed class SkillMaterializedPackageWriter : ISkillMaterializedPackageWriter
{
    private readonly ISkillPackageDirectoryOperations directoryOperations;

    /// <summary> Initializes a new instance of the <see cref="SkillMaterializedPackageWriter" /> class. </summary>
    /// <param name="directoryOperations"> The directory operations used by package transactions. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="directoryOperations" /> is <see langword="null" />. </exception>
    public SkillMaterializedPackageWriter (ISkillPackageDirectoryOperations directoryOperations)
    {
        this.directoryOperations = directoryOperations ?? throw new ArgumentNullException(nameof(directoryOperations));
    }

    /// <inheritdoc />
    public async ValueTask<AgentDistributionOperationResult<bool>> WriteAsync (
        SkillMaterializedPackageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var transactionResult = CreateTransaction(request);
        if (!transactionResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(transactionResult.Failure!.Code, transactionResult.Failure.Message);
        }

        return await ExecuteTransactionAsync(
            transactionResult.Value!,
            cancellationToken).ConfigureAwait(false);
    }

    private static AgentDistributionOperationResult<SkillPackageWriteTransaction> CreateTransaction (
        SkillMaterializedPackageWriteRequest request)
    {
        var skillDirectoryResult = PackagePathResolver.ResolveUnderRoot(request.TargetRoot, request.SkillDirectory);
        if (!skillDirectoryResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillPackageWriteTransaction>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
        }

        return CreateTransactionForSkillDirectory(request, skillDirectoryResult.Value!);
    }

    private static AgentDistributionOperationResult<SkillPackageWriteTransaction> CreateTransactionForSkillDirectory (
        SkillMaterializedPackageWriteRequest request,
        AbsolutePath resolvedSkillDirectory)
    {
        if (!resolvedSkillDirectory.TryGetParent(out var parentDirectory))
        {
            return AgentDistributionOperationResult<SkillPackageWriteTransaction>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetWriteFailed,
                $"Skill directory parent could not be resolved: {resolvedSkillDirectory}");
        }

        var transactionRootResult = PackagePathResolver.ResolveUnderRoot(
            request.TargetRoot,
            ContainedPath.Create(parentDirectory, RootRelativePath.Parse(".agent-distribution-skill-transactions")).Target);
        if (!transactionRootResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillPackageWriteTransaction>.FailureResult(transactionRootResult.Failure!.Code, transactionRootResult.Failure.Message);
        }

        return CreateTransactionDirectories(request, resolvedSkillDirectory, transactionRootResult.Value!);
    }

    private static AgentDistributionOperationResult<SkillPackageWriteTransaction> CreateTransactionDirectories (
        SkillMaterializedPackageWriteRequest request,
        AbsolutePath resolvedSkillDirectory,
        AbsolutePath transactionLockRoot)
    {
        var workspaceResult = ResolveTransactionDirectory(
            request.TargetRoot,
            transactionLockRoot,
            Guid.NewGuid().ToString("N"));
        if (!workspaceResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillPackageWriteTransaction>.FailureResult(workspaceResult.Failure!.Code, workspaceResult.Failure.Message);
        }

        // NOTE: Dotted names are outside the SkillName contract, reserving these siblings for transaction data.
        var stagingDirectoryResult = ResolveTransactionDirectory(
            request.TargetRoot,
            workspaceResult.Value!,
            ".staging");
        if (!stagingDirectoryResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillPackageWriteTransaction>.FailureResult(stagingDirectoryResult.Failure!.Code, stagingDirectoryResult.Failure.Message);
        }

        var backupContainerResult = ResolveTransactionDirectory(
            request.TargetRoot,
            workspaceResult.Value!,
            ".backup");
        if (!backupContainerResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillPackageWriteTransaction>.FailureResult(backupContainerResult.Failure!.Code, backupContainerResult.Failure.Message);
        }

        var backupDirectoryResult = ResolveTransactionDirectory(
            request.TargetRoot,
            backupContainerResult.Value!,
            Path.GetFileName(resolvedSkillDirectory.Value));
        if (!backupDirectoryResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillPackageWriteTransaction>.FailureResult(backupDirectoryResult.Failure!.Code, backupDirectoryResult.Failure.Message);
        }

        return AgentDistributionOperationResult<SkillPackageWriteTransaction>.Success(new SkillPackageWriteTransaction(
            request,
            resolvedSkillDirectory,
            transactionLockRoot,
            workspaceResult.Value!,
            stagingDirectoryResult.Value!,
            backupContainerResult.Value!,
            backupDirectoryResult.Value!));
    }

    private static AgentDistributionOperationResult<AbsolutePath> ResolveTransactionDirectory (
        AbsolutePath targetRoot,
        AbsolutePath parentDirectory,
        string directoryName)
    {
        return PackagePathResolver.ResolveUnderRoot(
            targetRoot,
            ContainedPath.Create(parentDirectory, RootRelativePath.Parse(directoryName)).Target);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> ExecuteTransactionAsync (
        SkillPackageWriteTransaction transaction,
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
                return CreateWriteFailureResult(transaction, ex);
            }

            throw;
        }
        finally
        {
            CleanupTransaction(transaction);
        }
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> ExecuteWithinTransactionAsync (
        SkillPackageWriteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var lockResult = AcquireTransactionLock(transaction);
        if (!lockResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(lockResult.Failure!.Code, lockResult.Failure.Message);
        }

        using var transactionLock = lockResult.Value!;
        return await WriteLockedAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    private AgentDistributionOperationResult<IDisposable> AcquireTransactionLock (SkillPackageWriteTransaction transaction)
    {
        var transactionLockRootResult = CreateVerifiedDirectory(transaction.TargetRoot, transaction.TransactionLockRoot);
        if (!transactionLockRootResult.IsSuccess)
        {
            return AgentDistributionOperationResult<IDisposable>.FailureResult(transactionLockRootResult.Failure!.Code, transactionLockRootResult.Failure.Message);
        }

        return SkillPackageTransactionLock.Acquire(transaction.TargetRoot, transaction.TransactionLockRoot);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> WriteLockedAsync (
        SkillPackageWriteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var stagedPackageResult = await CreateStagedPackageAsync(transaction, cancellationToken).ConfigureAwait(false);
        if (!stagedPackageResult.IsSuccess)
        {
            return stagedPackageResult;
        }

        var preCommitResult = await VerifyPreCommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        if (!preCommitResult.IsSuccess)
        {
            return preCommitResult;
        }

        return await CommitStagedPackageAsync(transaction, preCommitResult.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> CreateStagedPackageAsync (
        SkillPackageWriteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var workspaceResult = CreateVerifiedDirectory(transaction.TargetRoot, transaction.WorkspaceDirectory);
        if (!workspaceResult.IsSuccess)
        {
            return workspaceResult;
        }

        var stagingDirectoryResult = CreateVerifiedDirectory(transaction.TargetRoot, transaction.StagingDirectory);
        if (!stagingDirectoryResult.IsSuccess)
        {
            return stagingDirectoryResult;
        }

        return await PublishPackageFilesAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> PublishPackageFilesAsync (
        SkillPackageWriteTransaction transaction,
        CancellationToken cancellationToken)
    {
        foreach (var file in transaction.Request.MaterializedPackage.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var publishResult = await PublishPackageFileAsync(transaction, file, cancellationToken).ConfigureAwait(false);
            if (!publishResult.IsSuccess)
            {
                return publishResult;
            }
        }

        return AgentDistributionOperationResult<bool>.Success(true);
    }

    private static async ValueTask<AgentDistributionOperationResult<bool>> PublishPackageFileAsync (
        SkillPackageWriteTransaction transaction,
        PackageTextFile file,
        CancellationToken cancellationToken)
    {
        var finalPathResult = ResolvePackageFilePath(transaction.TargetRoot, transaction.SkillDirectory, file.RelativePath.RootRelativePath);
        if (!finalPathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(finalPathResult.Failure!.Code, finalPathResult.Failure.Message);
        }

        var stagingPathResult = ResolvePackageFilePath(transaction.TargetRoot, transaction.StagingDirectory, file.RelativePath.RootRelativePath);
        if (!stagingPathResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(stagingPathResult.Failure!.Code, stagingPathResult.Failure.Message);
        }

        await CanonicalTextFilePublisher.PublishAsync(stagingPathResult.Value!, file.Content, cancellationToken).ConfigureAwait(false);
        return AgentDistributionOperationResult<bool>.Success(true);
    }

    private static AgentDistributionOperationResult<AbsolutePath> ResolvePackageFilePath (
        AbsolutePath targetRoot,
        AbsolutePath packageDirectory,
        RootRelativePath relativePath)
    {
        return PackagePathResolver.ResolveUnderRoot(
            targetRoot,
            ContainedPath.Create(packageDirectory, relativePath).Target);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> VerifyPreCommitAsync (
        SkillPackageWriteTransaction transaction,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var preconditionResult = await InvokePreconditionAsync(transaction, transaction.SkillDirectory, cancellationToken).ConfigureAwait(false);
        if (!preconditionResult.IsSuccess)
        {
            return preconditionResult;
        }

        return VerifyWriteMode(transaction.SkillDirectory, transaction.Request.WriteMode);
    }

    private AgentDistributionOperationResult<bool> VerifyWriteMode (
        AbsolutePath skillDirectory,
        SkillMaterializedPackageWriteMode writeMode)
    {
        var targetExists = directoryOperations.Exists(skillDirectory);
        if ((writeMode == SkillMaterializedPackageWriteMode.CreateNew && targetExists)
            || (writeMode == SkillMaterializedPackageWriteMode.ReplaceExisting && !targetExists))
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetDigestMismatch,
                $"Target skill directory changed after planning; refusing to write: {skillDirectory}");
        }

        return AgentDistributionOperationResult<bool>.Success(targetExists);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> CommitStagedPackageAsync (
        SkillPackageWriteTransaction transaction,
        bool targetExists,
        CancellationToken cancellationToken)
    {
        if (targetExists)
        {
            var moveExistingResult = await MoveExistingTargetToBackupAsync(transaction, cancellationToken).ConfigureAwait(false);
            if (!moveExistingResult.IsSuccess)
            {
                return moveExistingResult;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        directoryOperations.Move(transaction.StagingDirectory, transaction.SkillDirectory);
        transaction.MarkCommitted();
        return AgentDistributionOperationResult<bool>.Success(true);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> MoveExistingTargetToBackupAsync (
        SkillPackageWriteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var backupContainerResult = CreateVerifiedDirectory(transaction.TargetRoot, transaction.BackupContainerDirectory);
        if (!backupContainerResult.IsSuccess)
        {
            return backupContainerResult;
        }

        directoryOperations.Move(transaction.SkillDirectory, transaction.BackupDirectory);
        transaction.MarkExistingTargetMovedToBackup();
        return await ValidateMovedTargetAsync(transaction, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> ValidateMovedTargetAsync (
        SkillPackageWriteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var preconditionResult = await InvokePreconditionAsync(transaction, transaction.BackupDirectory, cancellationToken).ConfigureAwait(false);
        return preconditionResult.IsSuccess
            ? preconditionResult
            : RestoreTargetBeforeReturningFailure(transaction, preconditionResult);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> InvokePreconditionAsync (
        SkillPackageWriteTransaction transaction,
        AbsolutePath path,
        CancellationToken cancellationToken)
    {
        return transaction.Request.Precondition is null
            ? AgentDistributionOperationResult<bool>.Success(true)
            : await transaction.Request.Precondition(path, cancellationToken).ConfigureAwait(false);
    }

    private AgentDistributionOperationResult<bool> RestoreTargetBeforeReturningFailure (
        SkillPackageWriteTransaction transaction,
        AgentDistributionOperationResult<bool> failureResult)
    {
        try
        {
            directoryOperations.Move(transaction.BackupDirectory, transaction.SkillDirectory);
            transaction.MarkExistingTargetRestored();
            return failureResult;
        }
        catch (Exception restoreException) when (restoreException is IOException or UnauthorizedAccessException)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetWriteFailed,
                $"Failed to write SKILL package atomically and restore backup: {transaction.SkillDirectory}. Backup remains at: {transaction.BackupDirectory}. {restoreException.Message}");
        }
    }

    private AgentDistributionOperationResult<bool> CreateWriteFailureResult (
        SkillPackageWriteTransaction transaction,
        Exception writeException)
    {
        return AgentDistributionOperationResult<bool>.FailureResult(
            AgentDistributionFailureCodes.InstallTargetWriteFailed,
            $"Failed to write SKILL package atomically: {transaction.SkillDirectory}. {writeException.Message}");
    }

    private AgentDistributionOperationResult<bool> RestoreMovedTargetIfNeeded (SkillPackageWriteTransaction transaction)
    {
        if (transaction.Committed
            || !transaction.ExistingTargetMovedToBackup
            || directoryOperations.Exists(transaction.SkillDirectory)
            || !directoryOperations.Exists(transaction.BackupDirectory))
        {
            return AgentDistributionOperationResult<bool>.Success(true);
        }

        try
        {
            directoryOperations.Move(transaction.BackupDirectory, transaction.SkillDirectory);
            transaction.MarkExistingTargetRestored();
            return AgentDistributionOperationResult<bool>.Success(true);
        }
        catch (Exception restoreException) when (restoreException is IOException or UnauthorizedAccessException)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetWriteFailed,
                $"Failed to write SKILL package atomically and restore backup: {transaction.SkillDirectory}. Backup remains at: {transaction.BackupDirectory}. {restoreException.Message}");
        }
    }

    private void CleanupTransaction (SkillPackageWriteTransaction transaction)
    {
        var preserveBackup = !transaction.Committed
            && transaction.ExistingTargetMovedToBackup
            && directoryOperations.Exists(transaction.BackupDirectory);
        DeleteDirectoryBestEffort(transaction.StagingDirectory);
        if (transaction.Committed || !transaction.ExistingTargetMovedToBackup)
        {
            DeleteDirectoryBestEffort(transaction.BackupDirectory);
        }

        if (!preserveBackup)
        {
            DeleteDirectoryBestEffort(transaction.WorkspaceDirectory);
        }
    }

    private AgentDistributionOperationResult<bool> CreateVerifiedDirectory (
        AbsolutePath targetRoot,
        AbsolutePath directory)
    {
        directoryOperations.Create(directory);
        return SkillPackageTransactionPathGuard.ValidateCreatedDirectory(targetRoot, directory);
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

    private sealed class SkillPackageWriteTransaction
    {
        public SkillPackageWriteTransaction (
            SkillMaterializedPackageWriteRequest request,
            AbsolutePath skillDirectory,
            AbsolutePath transactionLockRoot,
            AbsolutePath workspaceDirectory,
            AbsolutePath stagingDirectory,
            AbsolutePath backupContainerDirectory,
            AbsolutePath backupDirectory)
        {
            Request = request;
            SkillDirectory = skillDirectory;
            TransactionLockRoot = transactionLockRoot;
            WorkspaceDirectory = workspaceDirectory;
            StagingDirectory = stagingDirectory;
            BackupContainerDirectory = backupContainerDirectory;
            BackupDirectory = backupDirectory;
        }

        public SkillMaterializedPackageWriteRequest Request { get; }

        public AbsolutePath TargetRoot => Request.TargetRoot;

        public AbsolutePath SkillDirectory { get; }

        public AbsolutePath TransactionLockRoot { get; }

        public AbsolutePath WorkspaceDirectory { get; }

        public AbsolutePath StagingDirectory { get; }

        public AbsolutePath BackupContainerDirectory { get; }

        public AbsolutePath BackupDirectory { get; }

        public bool ExistingTargetMovedToBackup { get; private set; }

        public bool Committed { get; private set; }

        public void MarkExistingTargetMovedToBackup ()
        {
            ExistingTargetMovedToBackup = true;
        }

        public void MarkExistingTargetRestored ()
        {
            ExistingTargetMovedToBackup = false;
        }

        public void MarkCommitted ()
        {
            Committed = true;
        }
    }
}

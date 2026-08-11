using MackySoft.AgentDistribution.Installation.Contracts;
using MackySoft.AgentDistribution.Materialization;
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
        AbsolutePath targetRoot,
        AbsolutePath skillDirectory,
        SkillMaterializedPackage materializedPackage,
        SkillMaterializedPackageWriteMode writeMode,
        Func<AbsolutePath, CancellationToken, ValueTask<AgentDistributionOperationResult<bool>>>? precondition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetRoot);
        ArgumentNullException.ThrowIfNull(skillDirectory);
        ArgumentNullException.ThrowIfNull(materializedPackage);
        cancellationToken.ThrowIfCancellationRequested();

        var skillDirectoryResult = PackagePathResolver.ResolveUnderRoot(targetRoot, skillDirectory);
        if (!skillDirectoryResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
        }

        var resolvedSkillDirectory = skillDirectoryResult.Value!;
        if (!resolvedSkillDirectory.TryGetParent(out var parentDirectory))
        {
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetWriteFailed,
                $"Skill directory parent could not be resolved: {resolvedSkillDirectory}");
        }

        var transactionRootResult = PackagePathResolver.ResolveUnderRoot(
            targetRoot,
            ContainedPath.Create(parentDirectory, RootRelativePath.Parse(".agent-distribution-skill-transactions")).Target);
        if (!transactionRootResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(transactionRootResult.Failure!.Code, transactionRootResult.Failure.Message);
        }

        var transactionRoot = transactionRootResult.Value!;
        var stagingDirectoryResult = PackagePathResolver.ResolveUnderRoot(
            targetRoot,
            ContainedPath.Create(
                transactionRoot,
                RootRelativePath.Parse($"{Path.GetFileName(resolvedSkillDirectory.Value)}.staging.{Guid.NewGuid():N}")).Target);
        if (!stagingDirectoryResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(stagingDirectoryResult.Failure!.Code, stagingDirectoryResult.Failure.Message);
        }

        var backupContainerResult = PackagePathResolver.ResolveUnderRoot(
            targetRoot,
            ContainedPath.Create(
                transactionRoot,
                RootRelativePath.Parse($"{Path.GetFileName(resolvedSkillDirectory.Value)}.backup.{Guid.NewGuid():N}")).Target);
        if (!backupContainerResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(backupContainerResult.Failure!.Code, backupContainerResult.Failure.Message);
        }

        var backupDirectoryResult = PackagePathResolver.ResolveUnderRoot(
            targetRoot,
            ContainedPath.Create(
                backupContainerResult.Value!,
                RootRelativePath.Parse(Path.GetFileName(resolvedSkillDirectory.Value))).Target);
        if (!backupDirectoryResult.IsSuccess)
        {
            return AgentDistributionOperationResult<bool>.FailureResult(backupDirectoryResult.Failure!.Code, backupDirectoryResult.Failure.Message);
        }

        var stagingDirectory = stagingDirectoryResult.Value!;
        var backupContainer = backupContainerResult.Value!;
        var backupDirectory = backupDirectoryResult.Value!;
        var movedExistingToBackup = false;
        var committed = false;

        try
        {
            directoryOperations.Create(transactionRoot);
            var transactionRootGuard = SkillPackageTransactionPathGuard.ValidateCreatedDirectory(targetRoot, transactionRoot);
            if (!transactionRootGuard.IsSuccess)
            {
                return AgentDistributionOperationResult<bool>.FailureResult(transactionRootGuard.Failure!.Code, transactionRootGuard.Failure.Message);
            }

            var lockResult = SkillPackageTransactionLock.Acquire(targetRoot, transactionRoot);
            if (!lockResult.IsSuccess)
            {
                return AgentDistributionOperationResult<bool>.FailureResult(lockResult.Failure!.Code, lockResult.Failure.Message);
            }

            using var transactionLock = lockResult.Value!;
            directoryOperations.Create(stagingDirectory);
            var stagingDirectoryGuard = SkillPackageTransactionPathGuard.ValidateCreatedDirectory(targetRoot, stagingDirectory);
            if (!stagingDirectoryGuard.IsSuccess)
            {
                return AgentDistributionOperationResult<bool>.FailureResult(stagingDirectoryGuard.Failure!.Code, stagingDirectoryGuard.Failure.Message);
            }

            foreach (var file in materializedPackage.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var finalPathResult = PackagePathResolver.ResolveUnderRoot(
                    targetRoot,
                    ContainedPath.Create(resolvedSkillDirectory, file.RelativePath.RootRelativePath).Target);
                if (!finalPathResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<bool>.FailureResult(finalPathResult.Failure!.Code, finalPathResult.Failure.Message);
                }

                var stagingPathResult = PackagePathResolver.ResolveUnderRoot(
                    targetRoot,
                    ContainedPath.Create(stagingDirectory, file.RelativePath.RootRelativePath).Target);
                if (!stagingPathResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<bool>.FailureResult(stagingPathResult.Failure!.Code, stagingPathResult.Failure.Message);
                }

                await CanonicalTextFilePublisher.PublishAsync(stagingPathResult.Value!, file.Content, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (precondition is not null)
            {
                var preconditionResult = await precondition(resolvedSkillDirectory, cancellationToken).ConfigureAwait(false);
                if (!preconditionResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<bool>.FailureResult(preconditionResult.Failure!.Code, preconditionResult.Failure.Message);
                }
            }

            var targetExists = directoryOperations.Exists(resolvedSkillDirectory);
            if (writeMode == SkillMaterializedPackageWriteMode.CreateNew && targetExists)
            {
                return AgentDistributionOperationResult<bool>.FailureResult(
                    AgentDistributionFailureCodes.InstallTargetDigestMismatch,
                    $"Target skill directory changed after planning; refusing to write: {resolvedSkillDirectory}");
            }

            if (writeMode == SkillMaterializedPackageWriteMode.ReplaceExisting && !targetExists)
            {
                return AgentDistributionOperationResult<bool>.FailureResult(
                    AgentDistributionFailureCodes.InstallTargetDigestMismatch,
                    $"Target skill directory changed after planning; refusing to write: {resolvedSkillDirectory}");
            }

            if (targetExists)
            {
                directoryOperations.Create(backupContainer);
                var backupContainerGuard = SkillPackageTransactionPathGuard.ValidateCreatedDirectory(targetRoot, backupContainer);
                if (!backupContainerGuard.IsSuccess)
                {
                    return AgentDistributionOperationResult<bool>.FailureResult(backupContainerGuard.Failure!.Code, backupContainerGuard.Failure.Message);
                }

                directoryOperations.Move(resolvedSkillDirectory, backupDirectory);
                movedExistingToBackup = true;
                if (precondition is not null)
                {
                    var movedTargetResult = await precondition(backupDirectory, cancellationToken).ConfigureAwait(false);
                    if (!movedTargetResult.IsSuccess)
                    {
                        try
                        {
                            directoryOperations.Move(backupDirectory, resolvedSkillDirectory);
                            movedExistingToBackup = false;
                        }
                        catch (Exception restoreException) when (restoreException is IOException or UnauthorizedAccessException)
                        {
                            return AgentDistributionOperationResult<bool>.FailureResult(
                                AgentDistributionFailureCodes.InstallTargetWriteFailed,
                                $"Failed to write SKILL package atomically and restore backup: {resolvedSkillDirectory}. Backup remains at: {backupDirectory}. {restoreException.Message}");
                        }

                        return AgentDistributionOperationResult<bool>.FailureResult(movedTargetResult.Failure!.Code, movedTargetResult.Failure.Message);
                    }
                }
            }

            directoryOperations.Move(stagingDirectory, resolvedSkillDirectory);
            committed = true;
            DeleteDirectoryBestEffort(backupDirectory);

            return AgentDistributionOperationResult<bool>.Success(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!committed && movedExistingToBackup && !directoryOperations.Exists(resolvedSkillDirectory) && directoryOperations.Exists(backupDirectory))
            {
                try
                {
                    directoryOperations.Move(backupDirectory, resolvedSkillDirectory);
                }
                catch (Exception restoreException) when (restoreException is IOException or UnauthorizedAccessException)
                {
                    return AgentDistributionOperationResult<bool>.FailureResult(
                        AgentDistributionFailureCodes.InstallTargetWriteFailed,
                        $"Failed to write SKILL package atomically and restore backup: {resolvedSkillDirectory}. Backup remains at: {backupDirectory}. {restoreException.Message}");
                }
            }

            var backupMessage = !committed && movedExistingToBackup && directoryOperations.Exists(backupDirectory)
                ? $" Backup remains at: {backupDirectory}."
                : string.Empty;
            return AgentDistributionOperationResult<bool>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetWriteFailed,
                $"Failed to write SKILL package atomically: {resolvedSkillDirectory}.{backupMessage} {ex.Message}");
        }
        finally
        {
            var preserveBackup = !committed && movedExistingToBackup && directoryOperations.Exists(backupDirectory);
            DeleteDirectoryBestEffort(stagingDirectory);
            if (committed || !movedExistingToBackup)
            {
                DeleteDirectoryBestEffort(backupDirectory);
            }

            if (!preserveBackup)
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

using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Publishes one generated bundle and its matching source definition as a rollback unit. </summary>
internal sealed class SourceAndGeneratedBundleTransaction
{
    private readonly Func<AbsolutePath, string, CancellationToken, ValueTask> publishSource;

    /// <summary> Initializes the source-publication execution boundary used by this transaction. </summary>
    internal SourceAndGeneratedBundleTransaction (
        Func<AbsolutePath, string, CancellationToken, ValueTask> publishSource)
    {
        this.publishSource = publishSource ?? throw new ArgumentNullException(nameof(publishSource));
    }

    /// <summary> Replaces generated output, publishes source text, and restores generated output when source publication fails. </summary>
    internal async ValueTask<SkillOperationResult<AbsolutePath>> PublishAsync (
        AbsolutePath bundleRoot,
        string sourceContents,
        Func<AbsolutePath, CancellationToken, ValueTask<SkillOperationResult<AbsolutePath>>> publishGenerated,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundleRoot);
        ArgumentNullException.ThrowIfNull(sourceContents);
        ArgumentNullException.ThrowIfNull(publishGenerated);
        cancellationToken.ThrowIfCancellationRequested();

        var generatedRoot = ContainedPath.Create(bundleRoot, RootRelativePath.Parse("generated")).Target;
        var sourceBundlePath = ContainedPath.Create(bundleRoot, RootRelativePath.Parse("bundle.json")).Target;
        var backupRoot = ContainedPath.Create(bundleRoot, RootRelativePath.Parse($".generated.build-backup.{Guid.NewGuid():N}")).Target;
        AbsolutePath? previousGeneratedRoot = null;
        var publicationStarted = false;

        try
        {
            var initialGeneratedRootResult = ValidateDirectoryOrMissing(generatedRoot, "Generated bundle output");
            if (!initialGeneratedRootResult.IsSuccess)
            {
                return SkillOperationResult<AbsolutePath>.FailureResult(
                    initialGeneratedRootResult.Failure!.Code,
                    initialGeneratedRootResult.Failure.Message);
            }

            if (initialGeneratedRootResult.Value!)
            {
                Directory.Move(generatedRoot.Value, backupRoot.Value);
                previousGeneratedRoot = backupRoot;
            }

            publicationStarted = true;
            var generatedResult = await publishGenerated(generatedRoot, cancellationToken).ConfigureAwait(false);
            if (!generatedResult.IsSuccess)
            {
                RestoreGeneratedBundle(generatedRoot, previousGeneratedRoot);
                publicationStarted = false;
                return generatedResult;
            }

            await publishSource(sourceBundlePath, sourceContents, cancellationToken).ConfigureAwait(false);

            publicationStarted = false;
            if (previousGeneratedRoot is not null)
            {
                TryDeleteDirectory(previousGeneratedRoot);
            }

            return generatedResult;
        }
        catch (Exception publicationException)
        {
            if (publicationStarted)
            {
                try
                {
                    RestoreGeneratedBundle(generatedRoot, previousGeneratedRoot);
                }
                catch (Exception rollbackException)
                {
                    var rollbackLocation = previousGeneratedRoot ?? generatedRoot;
                    throw new IOException(
                        $"Bundle publication and rollback failed. Inspect the generated bundle state at: {rollbackLocation}",
                        new AggregateException(publicationException, rollbackException));
                }
            }

            throw;
        }
    }

    private static void RestoreGeneratedBundle (
        AbsolutePath generatedRoot,
        AbsolutePath? previousGeneratedRoot)
    {
        var generatedRootResult = ValidateDirectoryOrMissing(generatedRoot, "Generated bundle output during rollback");
        if (!generatedRootResult.IsSuccess)
        {
            throw new IOException(generatedRootResult.Failure!.Message);
        }

        if (generatedRootResult.Value!)
        {
            Directory.Delete(generatedRoot.Value, recursive: true);
        }

        if (previousGeneratedRoot is null)
        {
            return;
        }

        var backupRootResult = ValidateDirectoryOrMissing(previousGeneratedRoot, "Generated bundle backup during rollback");
        if (!backupRootResult.IsSuccess)
        {
            throw new IOException(backupRootResult.Failure!.Message);
        }

        if (!backupRootResult.Value!)
        {
            throw new DirectoryNotFoundException($"Previous generated bundle backup is missing: {previousGeneratedRoot}");
        }

        Directory.Move(previousGeneratedRoot.Value, generatedRoot.Value);
    }

    private static void TryDeleteDirectory (AbsolutePath path)
    {
        try
        {
            var pathResult = ValidateDirectoryOrMissing(path, "Generated bundle backup cleanup");
            if (pathResult.IsSuccess && pathResult.Value!)
            {
                Directory.Delete(path.Value, recursive: true);
            }
        }
        catch (IOException)
        {
            // Cleanup after a successful transaction is best effort; source and generated output already agree.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup after a successful transaction is best effort; source and generated output already agree.
        }
    }

    private static SkillOperationResult<bool> ValidateDirectoryOrMissing (
        AbsolutePath path,
        string description)
    {
        if (!FileSystemEntryInspector.TryInspect(path, out var observation, out _))
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"{description} could not be inspected: {path.Value}");
        }

        return observation.State switch
        {
            FileSystemEntryState.Missing => SkillOperationResult<bool>.Success(false),
            FileSystemEntryState.Directory => SkillOperationResult<bool>.Success(true),
            _ => SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"{description} must be a regular directory or a missing path: {path.Value}"),
        };
    }
}

using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Coordinates generated bundle publication and source definition updates with rollback. </summary>
internal sealed class BundleBuildPublisher<TBundle>
    where TBundle : class
{
    private readonly Func<TBundle, string, CancellationToken, ValueTask<SkillOperationResult<string>>> writeGeneratedBundle;
    private readonly ISkillBundleBuildFileSystem fileSystem;

    /// <summary> Initializes one bundle publication boundary. </summary>
    internal BundleBuildPublisher (
        Func<TBundle, string, CancellationToken, ValueTask<SkillOperationResult<string>>> writeGeneratedBundle,
        ISkillBundleBuildFileSystem fileSystem)
    {
        this.writeGeneratedBundle = writeGeneratedBundle ?? throw new ArgumentNullException(nameof(writeGeneratedBundle));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <summary> Atomically replaces generated output without changing the source definition. </summary>
    internal ValueTask<SkillOperationResult<string>> PublishGeneratedAsync (
        TBundle bundle,
        string generatedRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentException.ThrowIfNullOrWhiteSpace(generatedRoot);
        return writeGeneratedBundle(bundle, generatedRoot, cancellationToken);
    }

    /// <summary> Publishes generated output and its matching source definition as one rollback boundary. </summary>
    internal async ValueTask<SkillOperationResult<string>> PublishSourceAndGeneratedAsync (
        string bundleRoot,
        string sourceContents,
        TBundle bundle,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentNullException.ThrowIfNull(sourceContents);
        ArgumentNullException.ThrowIfNull(bundle);
        cancellationToken.ThrowIfCancellationRequested();

        var fullBundleRoot = Path.GetFullPath(bundleRoot);
        var generatedRoot = Path.Combine(fullBundleRoot, "generated");
        var sourceBundlePath = Path.Combine(fullBundleRoot, "bundle.json");
        var backupRoot = Path.Combine(fullBundleRoot, $".generated.build-backup.{Guid.NewGuid():N}");
        string? previousGeneratedRoot = null;
        var publicationStarted = false;

        try
        {
            if (fileSystem.DirectoryExists(generatedRoot))
            {
                fileSystem.MoveDirectory(generatedRoot, backupRoot);
                previousGeneratedRoot = backupRoot;
            }

            publicationStarted = true;
            var generatedResult = await writeGeneratedBundle(bundle, generatedRoot, cancellationToken).ConfigureAwait(false);
            if (!generatedResult.IsSuccess)
            {
                RestoreGeneratedBundle(generatedRoot, previousGeneratedRoot);
                publicationStarted = false;
                return generatedResult;
            }

            await fileSystem.WriteSourceBundleAsync(sourceBundlePath, sourceContents, cancellationToken).ConfigureAwait(false);

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

    private void RestoreGeneratedBundle (
        string generatedRoot,
        string? previousGeneratedRoot)
    {
        if (fileSystem.DirectoryExists(generatedRoot))
        {
            fileSystem.DeleteDirectory(generatedRoot);
        }

        if (previousGeneratedRoot is null)
        {
            return;
        }

        if (!fileSystem.DirectoryExists(previousGeneratedRoot))
        {
            throw new DirectoryNotFoundException($"Previous generated bundle backup is missing: {previousGeneratedRoot}");
        }

        fileSystem.MoveDirectory(previousGeneratedRoot, generatedRoot);
    }

    private void TryDeleteDirectory (string path)
    {
        try
        {
            if (fileSystem.DirectoryExists(path))
            {
                fileSystem.DeleteDirectory(path);
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
}

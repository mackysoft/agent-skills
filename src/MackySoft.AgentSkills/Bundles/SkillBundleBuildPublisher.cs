using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Publishes generated output and coordinates source version updates with rollback. </summary>
internal sealed class SkillBundleBuildPublisher
{
    private readonly CanonicalSkillBundleWriter bundleWriter;
    private readonly SkillBundleJsonSerializer bundleSerializer;
    private readonly ISkillBundleBuildFileSystem fileSystem;

    /// <summary> Initializes one bundle build publication boundary. </summary>
    /// <param name="bundleWriter"> The generated bundle writer. </param>
    /// <param name="bundleSerializer"> The canonical source bundle serializer. </param>
    /// <param name="fileSystem"> The file-system transaction primitives. </param>
    internal SkillBundleBuildPublisher (
        CanonicalSkillBundleWriter bundleWriter,
        SkillBundleJsonSerializer bundleSerializer,
        ISkillBundleBuildFileSystem fileSystem)
    {
        this.bundleWriter = bundleWriter ?? throw new ArgumentNullException(nameof(bundleWriter));
        this.bundleSerializer = bundleSerializer ?? throw new ArgumentNullException(nameof(bundleSerializer));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    /// <summary> Atomically replaces generated output without changing the authored source definition. </summary>
    internal ValueTask<SkillOperationResult<string>> PublishGeneratedAsync (
        CanonicalSkillBundle bundle,
        string generatedRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentException.ThrowIfNullOrWhiteSpace(generatedRoot);
        return bundleWriter.WriteAsync(bundle, generatedRoot, cancellationToken);
    }

    /// <summary> Publishes generated output and its matching authored version as one rollback boundary. </summary>
    internal async ValueTask<SkillOperationResult<string>> PublishSourceAndGeneratedAsync (
        string bundleRoot,
        SkillBundleDefinition sourceDefinition,
        CanonicalSkillBundle bundle,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentNullException.ThrowIfNull(sourceDefinition);
        ArgumentNullException.ThrowIfNull(bundle);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateMatchingIdentity(sourceDefinition, bundle.Descriptor);

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
            var generatedResult = await bundleWriter.WriteAsync(bundle, generatedRoot, cancellationToken).ConfigureAwait(false);
            if (!generatedResult.IsSuccess)
            {
                RestoreGeneratedBundle(generatedRoot, previousGeneratedRoot);
                publicationStarted = false;
                return generatedResult;
            }

            await fileSystem.WriteSourceBundleAsync(
                    sourceBundlePath,
                    bundleSerializer.SerializeDefinition(sourceDefinition),
                    cancellationToken)
                .ConfigureAwait(false);

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
                        $"SKILL bundle publication and rollback failed. Inspect the generated bundle state at: {rollbackLocation}",
                        new AggregateException(publicationException, rollbackException));
                }
            }

            throw;
        }
    }

    private static void ValidateMatchingIdentity (
        SkillBundleDefinition sourceDefinition,
        SkillBundleDescriptor descriptor)
    {
        if (sourceDefinition.SchemaVersion != descriptor.SchemaVersion
            || sourceDefinition.CatalogId != descriptor.CatalogId
            || sourceDefinition.SkillBundleVersion != descriptor.SkillBundleVersion)
        {
            throw new ArgumentException("Source and generated bundle identities must match before publication.", nameof(sourceDefinition));
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
            throw new DirectoryNotFoundException($"Previous generated SKILL bundle backup is missing: {previousGeneratedRoot}");
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

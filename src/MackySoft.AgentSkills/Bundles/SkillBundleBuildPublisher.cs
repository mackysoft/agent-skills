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
        if (!fileSystem.DirectoryExists(generatedRoot))
        {
            throw new DirectoryNotFoundException($"Generated SKILL bundle disappeared before publication: {generatedRoot}");
        }

        var sourceBundlePath = Path.Combine(fullBundleRoot, "bundle.json");
        var backupRoot = Path.Combine(fullBundleRoot, $".generated.build-backup.{Guid.NewGuid():N}");
        var backupCreated = false;

        try
        {
            fileSystem.MoveDirectory(generatedRoot, backupRoot);
            backupCreated = true;

            var generatedResult = await bundleWriter.WriteAsync(bundle, generatedRoot, cancellationToken).ConfigureAwait(false);
            if (!generatedResult.IsSuccess)
            {
                RestoreGeneratedBundle(generatedRoot, backupRoot);
                backupCreated = false;
                return generatedResult;
            }

            await fileSystem.WriteSourceBundleAsync(
                    sourceBundlePath,
                    bundleSerializer.SerializeDefinition(sourceDefinition),
                    cancellationToken)
                .ConfigureAwait(false);

            backupCreated = false;
            TryDeleteDirectory(backupRoot);
            return generatedResult;
        }
        catch (Exception publicationException)
        {
            if (backupCreated)
            {
                try
                {
                    RestoreGeneratedBundle(generatedRoot, backupRoot);
                }
                catch (Exception rollbackException)
                {
                    throw new IOException(
                        $"SKILL bundle publication and rollback failed. The previous generated bundle remains at: {backupRoot}",
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
        string backupRoot)
    {
        if (fileSystem.DirectoryExists(generatedRoot))
        {
            fileSystem.DeleteDirectory(generatedRoot);
        }

        if (!fileSystem.DirectoryExists(backupRoot))
        {
            throw new DirectoryNotFoundException($"Previous generated SKILL bundle backup is missing: {backupRoot}");
        }

        fileSystem.MoveDirectory(backupRoot, generatedRoot);
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

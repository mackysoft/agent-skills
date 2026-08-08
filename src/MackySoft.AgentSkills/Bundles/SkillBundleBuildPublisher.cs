using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Publishes generated output and coordinates source version updates with rollback. </summary>
internal sealed class SkillBundleBuildPublisher
{
    private readonly BundleBuildPublisher<CanonicalSkillBundle> publisher;
    private readonly SkillBundleJsonSerializer bundleSerializer;

    /// <summary> Initializes one bundle build publication boundary. </summary>
    /// <param name="bundleWriter"> The generated bundle writer. </param>
    /// <param name="bundleSerializer"> The canonical source bundle serializer. </param>
    /// <param name="fileSystem"> The file-system transaction primitives. </param>
    internal SkillBundleBuildPublisher (
        CanonicalSkillBundleWriter bundleWriter,
        SkillBundleJsonSerializer bundleSerializer,
        ISkillBundleBuildFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(bundleWriter);
        this.bundleSerializer = bundleSerializer ?? throw new ArgumentNullException(nameof(bundleSerializer));
        publisher = new BundleBuildPublisher<CanonicalSkillBundle>(bundleWriter.WriteAsync, fileSystem);
    }

    /// <summary> Atomically replaces generated output without changing the authored source definition. </summary>
    internal ValueTask<SkillOperationResult<string>> PublishGeneratedAsync (
        CanonicalSkillBundle bundle,
        string generatedRoot,
        CancellationToken cancellationToken)
    {
        return publisher.PublishGeneratedAsync(bundle, generatedRoot, cancellationToken);
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

        return await publisher.PublishSourceAndGeneratedAsync(
                bundleRoot,
                bundleSerializer.SerializeDefinition(sourceDefinition),
                bundle,
                cancellationToken)
            .ConfigureAwait(false);
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

}

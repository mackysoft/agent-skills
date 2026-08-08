namespace MackySoft.AgentSkills.Bundles;

/// <summary> Publishes generated v2 output and coordinates source version updates with rollback. </summary>
internal sealed class AgentSkillsBundleBuildPublisher
{
    private readonly BundleBuildPublisher<CanonicalAgentSkillsBundle> publisher;
    private readonly AgentSkillsBundleJsonSerializer serializer;

    /// <summary> Initializes one v2 bundle publication boundary. </summary>
    internal AgentSkillsBundleBuildPublisher (
        CanonicalAgentSkillsBundleWriter bundleWriter,
        AgentSkillsBundleJsonSerializer serializer,
        ISkillBundleBuildFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(bundleWriter);
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        publisher = new BundleBuildPublisher<CanonicalAgentSkillsBundle>(bundleWriter.WriteAsync, fileSystem);
    }

    /// <summary> Atomically replaces generated output without changing the source definition. </summary>
    internal ValueTask<Shared.SkillOperationResult<string>> PublishGeneratedAsync (
        CanonicalAgentSkillsBundle bundle,
        string generatedRoot,
        CancellationToken cancellationToken)
    {
        return publisher.PublishGeneratedAsync(bundle, generatedRoot, cancellationToken);
    }

    /// <summary> Publishes generated output and its matching source version as one rollback boundary. </summary>
    internal ValueTask<Shared.SkillOperationResult<string>> PublishSourceAndGeneratedAsync (
        string bundleRoot,
        AgentSkillsBundleDefinition sourceDefinition,
        CanonicalAgentSkillsBundle bundle,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        ArgumentNullException.ThrowIfNull(sourceDefinition);
        ArgumentNullException.ThrowIfNull(bundle);
        ValidateMatchingIdentity(sourceDefinition, bundle.Descriptor);

        return publisher.PublishSourceAndGeneratedAsync(
            bundleRoot,
            serializer.SerializeDefinition(sourceDefinition),
            bundle,
            cancellationToken);
    }

    private static void ValidateMatchingIdentity (
        AgentSkillsBundleDefinition sourceDefinition,
        AgentSkillsBundleDescriptor descriptor)
    {
        if (sourceDefinition.SchemaVersion != descriptor.SchemaVersion
            || sourceDefinition.CatalogId != descriptor.CatalogId
            || sourceDefinition.BundleVersion != descriptor.BundleVersion)
        {
            throw new ArgumentException("Source and generated v2 bundle identities must match before publication.", nameof(sourceDefinition));
        }
    }
}

using MackySoft.AgentDistribution.Agents.Generation;
using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Agents.Packaging;
using MackySoft.AgentDistribution.Agents.Sources;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Generation;
using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Serialization;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Reconciles source schema v2 and its generated mixed bundle. </summary>
public sealed class AgentDistributionBundleBuildService
{
    private readonly AgentDistributionBundleGenerationService generationService;
    private readonly CanonicalAgentDistributionBundleReader bundleReader;
    private readonly CanonicalAgentDistributionBundleWriter bundleWriter;
    private readonly AgentDistributionBundleJsonSerializer bundleSerializer;
    private readonly SourceAndGeneratedBundleTransaction transaction;

    private AgentDistributionBundleBuildService (
        AgentDistributionBundleGenerationService generationService,
        CanonicalAgentDistributionBundleReader bundleReader,
        CanonicalAgentDistributionBundleWriter bundleWriter,
        AgentDistributionBundleJsonSerializer bundleSerializer)
    {
        this.generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
        this.bundleWriter = bundleWriter ?? throw new ArgumentNullException(nameof(bundleWriter));
        this.bundleSerializer = bundleSerializer ?? throw new ArgumentNullException(nameof(bundleSerializer));
        transaction = new SourceAndGeneratedBundleTransaction(CanonicalTextFilePublisher.PublishAsync);
    }

    /// <summary> Creates the default v2 build service with all built-in host modules. </summary>
    public static AgentDistributionBundleBuildService CreateDefault ()
    {
        var skillManifestSerializer = new SkillManifestJsonSerializer();
        var digestCalculator = new SkillDigestCalculator();
        var skillGenerator = new SkillPackageGenerationService(
            new SkillBundleDefinitionReader(new SkillBundleJsonSerializer()),
            new Sources.SkillSourceDefinitionReader(),
            digestCalculator,
            skillManifestSerializer,
            new SkillManifest.Factory(new SkillManifestDigestCalculator(skillManifestSerializer)),
            new CanonicalSkillPackage.Factory(digestCalculator, skillManifestSerializer),
            new SkillBundleDigestCalculator(skillManifestSerializer),
            new CanonicalSkillBundle.Factory(new SkillBundleDigestCalculator(skillManifestSerializer)));
        var agentManifestSerializer = new AgentManifestJsonSerializer();
        var agentGenerator = new AgentPackageGenerationService(agentManifestSerializer, new AgentManifestDigestCalculator(agentManifestSerializer), digestCalculator);
        var mixedDigest = new AgentDistributionBundleDigestCalculator(skillManifestSerializer, agentManifestSerializer, digestCalculator);
        var agentReader = new CanonicalAgentPackageReader(
            agentManifestSerializer,
            digestCalculator,
            new AgentManifestDigestCalculator(agentManifestSerializer));
        var skillReader = new CanonicalSkillPackageReader(skillManifestSerializer, new SkillManifest.Factory(new SkillManifestDigestCalculator(skillManifestSerializer)), new CanonicalSkillPackage.Factory(digestCalculator, skillManifestSerializer));
        var mixedSerializer = new AgentDistributionBundleJsonSerializer();
        var mixedBundleReader = new CanonicalAgentDistributionBundleReader(
            mixedSerializer,
            skillReader,
            agentReader,
            mixedDigest);
        var mixedBundleWriter = new CanonicalAgentDistributionBundleWriter(
            new CanonicalSkillPackageWriter(),
            new CanonicalAgentPackageWriter(),
            mixedSerializer,
            mixedBundleReader);
        return new AgentDistributionBundleBuildService(
            new AgentDistributionBundleGenerationService(
                new AgentDistributionBundleDefinitionReader(mixedSerializer),
                new Sources.SkillSourceDefinitionReader(),
                new AgentSourceDefinitionReader(),
                skillGenerator,
                agentGenerator,
                mixedDigest),
            mixedBundleReader,
            mixedBundleWriter,
            mixedSerializer);
    }

    /// <summary> Builds v2 generated output at the authored or next explicit version. </summary>
    public async ValueTask<SkillOperationResult<AgentDistributionBundleBuildResult>> BuildAsync (
        string bundleRoot,
        int? bundleVersion,
        bool check,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var fullBundleRoot = AbsolutePath.Parse(Path.GetFullPath(bundleRoot));
        var sourceResult = await generationService.ReadSourceAsync(fullBundleRoot, cancellationToken).ConfigureAwait(false);
        if (!sourceResult.IsSuccess)
        {
            return SkillOperationResult<AgentDistributionBundleBuildResult>.FailureResult(sourceResult.Failure!.Code, sourceResult.Failure.Message);
        }

        var source = sourceResult.Value!;
        AgentDistributionBundleVersion target;
        if (bundleVersion is null)
        {
            target = source.BundleDefinition.BundleVersion;
        }
        else if (!AgentDistributionBundleVersion.TryCreate(bundleVersion.Value, out var requestedVersion))
        {
            return SkillOperationResult<AgentDistributionBundleBuildResult>.FailureResult(
                SkillFailureCodes.InputInvalid,
                $"bundleVersion must be a positive integer: {bundleVersion.Value}");
        }
        else
        {
            target = requestedVersion;
        }

        if (target.CompareTo(source.BundleDefinition.BundleVersion) < 0 || (target != source.BundleDefinition.BundleVersion && target != source.BundleDefinition.BundleVersion.Next()))
        {
            return SkillOperationResult<AgentDistributionBundleBuildResult>.FailureResult(SkillFailureCodes.InputInvalid, "bundleVersion must equal the authored version or its next revision.");
        }

        CanonicalAgentDistributionBundle candidate;
        try
        {
            candidate = generationService.Generate(source, target);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return SkillOperationResult<AgentDistributionBundleBuildResult>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                $"The v2 source bundle could not be generated: {exception.Message}");
        }
        var generatedRoot = ContainedPath.Create(fullBundleRoot, RootRelativePath.Parse("generated")).Target;
        if (!FileSystemEntryInspector.TryInspect(generatedRoot, out var generatedRootObservation, out _))
        {
            return SkillOperationResult<AgentDistributionBundleBuildResult>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Generated v2 bundle output could not be inspected: {generatedRoot}");
        }

        CanonicalAgentDistributionBundle? current = null;
        if (generatedRootObservation.State == FileSystemEntryState.Directory)
        {
            var currentResult = await bundleReader.ReadAsync(generatedRoot, cancellationToken).ConfigureAwait(false);
            if (!currentResult.IsSuccess)
            {
                return SkillOperationResult<AgentDistributionBundleBuildResult>.FailureResult(currentResult.Failure!.Code, currentResult.Failure.Message);
            }

            current = currentResult.Value!;
        }
        else if (generatedRootObservation.State != FileSystemEntryState.Missing)
        {
            return SkillOperationResult<AgentDistributionBundleBuildResult>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Generated v2 bundle output must be a regular directory: {generatedRoot}");
        }

        var sourceChanged = target != source.BundleDefinition.BundleVersion;
        if (!sourceChanged && current is not null && current.Descriptor.BundleVersion == target && current.Descriptor.BundleDigest == candidate.Descriptor.BundleDigest)
        {
            return SkillOperationResult<AgentDistributionBundleBuildResult>.Success(new AgentDistributionBundleBuildResult(false, candidate.Descriptor));
        }

        if (check)
        {
            return SkillOperationResult<AgentDistributionBundleBuildResult>.FailureResult(SkillFailureCodes.BundleUpdateRequired, "Canonical v2 bundle requires generation.");
        }

        SkillOperationResult<AbsolutePath> write;
        if (sourceChanged)
        {
            var updated = new AgentDistributionBundleDefinition(
                source.BundleDefinition.SchemaVersion,
                source.BundleDefinition.CatalogId,
                target);
            ValidatePublicationIdentity(updated, candidate.Descriptor);
            write = await transaction.PublishAsync(
                    fullBundleRoot,
                    bundleSerializer.SerializeDefinition(updated),
                    (outputRoot, token) => bundleWriter.WriteAsync(candidate, outputRoot, token),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            write = await bundleWriter.WriteAsync(candidate, generatedRoot, cancellationToken).ConfigureAwait(false);
        }

        if (!write.IsSuccess)
        {
            return SkillOperationResult<AgentDistributionBundleBuildResult>.FailureResult(write.Failure!.Code, write.Failure.Message);
        }

        return SkillOperationResult<AgentDistributionBundleBuildResult>.Success(new AgentDistributionBundleBuildResult(true, candidate.Descriptor));
    }

    private static void ValidatePublicationIdentity (
        AgentDistributionBundleDefinition sourceDefinition,
        AgentDistributionBundleDescriptor descriptor)
    {
        if (sourceDefinition.SchemaVersion != descriptor.SchemaVersion
            || sourceDefinition.CatalogId != descriptor.CatalogId
            || sourceDefinition.BundleVersion != descriptor.BundleVersion)
        {
            throw new ArgumentException("Source and generated v2 bundle identities must match before publication.", nameof(sourceDefinition));
        }
    }
}

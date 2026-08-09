using MackySoft.AgentSkills.Agents.Generation;
using MackySoft.AgentSkills.Agents.Manifests;
using MackySoft.AgentSkills.Agents.Packaging;
using MackySoft.AgentSkills.Agents.Sources;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Generation;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Serialization;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Reconciles source schema v2 and its generated mixed bundle. </summary>
public sealed class AgentSkillsBundleBuildService
{
    private readonly AgentSkillsBundleGenerationService generationService;
    private readonly CanonicalAgentSkillsBundleReader bundleReader;
    private readonly CanonicalAgentSkillsBundleWriter bundleWriter;
    private readonly AgentSkillsBundleJsonSerializer bundleSerializer;
    private readonly SourceAndGeneratedBundleTransaction transaction;

    private AgentSkillsBundleBuildService (
        AgentSkillsBundleGenerationService generationService,
        CanonicalAgentSkillsBundleReader bundleReader,
        CanonicalAgentSkillsBundleWriter bundleWriter,
        AgentSkillsBundleJsonSerializer bundleSerializer)
    {
        this.generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
        this.bundleWriter = bundleWriter ?? throw new ArgumentNullException(nameof(bundleWriter));
        this.bundleSerializer = bundleSerializer ?? throw new ArgumentNullException(nameof(bundleSerializer));
        transaction = new SourceAndGeneratedBundleTransaction(CanonicalTextFilePublisher.PublishAsync);
    }

    /// <summary> Creates the default v2 build service with all built-in host modules. </summary>
    public static AgentSkillsBundleBuildService CreateDefault ()
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
        var mixedDigest = new AgentSkillsBundleDigestCalculator(skillManifestSerializer, agentManifestSerializer, digestCalculator);
        var agentReader = new CanonicalAgentPackageReader(
            agentManifestSerializer,
            digestCalculator,
            new AgentManifestDigestCalculator(agentManifestSerializer));
        var skillReader = new CanonicalSkillPackageReader(skillManifestSerializer, new SkillManifest.Factory(new SkillManifestDigestCalculator(skillManifestSerializer)), new CanonicalSkillPackage.Factory(digestCalculator, skillManifestSerializer));
        var mixedSerializer = new AgentSkillsBundleJsonSerializer();
        var mixedBundleReader = new CanonicalAgentSkillsBundleReader(
            mixedSerializer,
            skillReader,
            agentReader,
            mixedDigest);
        var mixedBundleWriter = new CanonicalAgentSkillsBundleWriter(
            new CanonicalSkillPackageWriter(),
            new CanonicalAgentPackageWriter(),
            mixedSerializer,
            mixedBundleReader);
        return new AgentSkillsBundleBuildService(
            new AgentSkillsBundleGenerationService(
                new AgentSkillsBundleDefinitionReader(mixedSerializer),
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
    public async ValueTask<SkillOperationResult<AgentSkillsBundleBuildResult>> BuildAsync (
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
            return SkillOperationResult<AgentSkillsBundleBuildResult>.FailureResult(sourceResult.Failure!.Code, sourceResult.Failure.Message);
        }

        var source = sourceResult.Value!;
        AgentSkillsBundleVersion target;
        if (bundleVersion is null)
        {
            target = source.BundleDefinition.BundleVersion;
        }
        else if (!AgentSkillsBundleVersion.TryCreate(bundleVersion.Value, out var requestedVersion))
        {
            return SkillOperationResult<AgentSkillsBundleBuildResult>.FailureResult(
                SkillFailureCodes.InputInvalid,
                $"bundleVersion must be a positive integer: {bundleVersion.Value}");
        }
        else
        {
            target = requestedVersion;
        }

        if (target.CompareTo(source.BundleDefinition.BundleVersion) < 0 || (target != source.BundleDefinition.BundleVersion && target != source.BundleDefinition.BundleVersion.Next()))
        {
            return SkillOperationResult<AgentSkillsBundleBuildResult>.FailureResult(SkillFailureCodes.InputInvalid, "bundleVersion must equal the authored version or its next revision.");
        }

        CanonicalAgentSkillsBundle candidate;
        try
        {
            candidate = generationService.Generate(source, target);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return SkillOperationResult<AgentSkillsBundleBuildResult>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                $"The v2 source bundle could not be generated: {exception.Message}");
        }
        var generatedRoot = ContainedPath.Create(fullBundleRoot, RootRelativePath.Parse("generated")).Target;
        if (!FileSystemEntryInspector.TryInspect(generatedRoot, out var generatedRootObservation, out _))
        {
            return SkillOperationResult<AgentSkillsBundleBuildResult>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Generated v2 bundle output could not be inspected: {generatedRoot}");
        }

        CanonicalAgentSkillsBundle? current = null;
        if (generatedRootObservation.State == FileSystemEntryState.Directory)
        {
            var currentResult = await bundleReader.ReadAsync(generatedRoot, cancellationToken).ConfigureAwait(false);
            if (!currentResult.IsSuccess)
            {
                return SkillOperationResult<AgentSkillsBundleBuildResult>.FailureResult(currentResult.Failure!.Code, currentResult.Failure.Message);
            }

            current = currentResult.Value!;
        }
        else if (generatedRootObservation.State != FileSystemEntryState.Missing)
        {
            return SkillOperationResult<AgentSkillsBundleBuildResult>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Generated v2 bundle output must be a regular directory: {generatedRoot}");
        }

        var sourceChanged = target != source.BundleDefinition.BundleVersion;
        if (!sourceChanged && current is not null && current.Descriptor.BundleVersion == target && current.Descriptor.BundleDigest == candidate.Descriptor.BundleDigest)
        {
            return SkillOperationResult<AgentSkillsBundleBuildResult>.Success(new AgentSkillsBundleBuildResult(false, candidate.Descriptor));
        }

        if (check)
        {
            return SkillOperationResult<AgentSkillsBundleBuildResult>.FailureResult(SkillFailureCodes.BundleUpdateRequired, "Canonical v2 bundle requires generation.");
        }

        SkillOperationResult<AbsolutePath> write;
        if (sourceChanged)
        {
            var updated = new AgentSkillsBundleDefinition(
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
            return SkillOperationResult<AgentSkillsBundleBuildResult>.FailureResult(write.Failure!.Code, write.Failure.Message);
        }

        return SkillOperationResult<AgentSkillsBundleBuildResult>.Success(new AgentSkillsBundleBuildResult(true, candidate.Descriptor));
    }

    private static void ValidatePublicationIdentity (
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

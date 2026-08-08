using MackySoft.AgentSkills.Agents.Generation;
using MackySoft.AgentSkills.Agents.Manifests;
using MackySoft.AgentSkills.Agents.Packaging;
using MackySoft.AgentSkills.Agents.Sources;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Generation;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Reconciles source schema v2 and its generated mixed bundle. </summary>
public sealed class AgentSkillsBundleBuildService
{
    private readonly AgentSkillsBundleGenerationService generationService;
    private readonly CanonicalAgentSkillsBundleReader bundleReader;
    private readonly AgentSkillsBundleBuildPublisher publisher;

    private AgentSkillsBundleBuildService (
        AgentSkillsBundleGenerationService generationService,
        CanonicalAgentSkillsBundleReader bundleReader,
        AgentSkillsBundleBuildPublisher publisher)
    {
        this.generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    /// <summary> Creates the default v2 build service with no product-specific agent host adapters. </summary>
    public static AgentSkillsBundleBuildService CreateDefault ()
    {
        return Create(new SkillBundleBuildFileSystem());
    }

    /// <summary> Creates the v2 build service with an explicit publication file system. </summary>
    internal static AgentSkillsBundleBuildService Create (ISkillBundleBuildFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        var skillManifestSerializer = new SkillManifestJsonSerializer();
        var skillHosts = new SkillHostAdapterSet();
        var digestCalculator = new SkillDigestCalculator();
        var skillGenerator = new SkillPackageGenerationService(
            new SkillBundleDefinitionReader(new SkillBundleJsonSerializer()),
            new Sources.SkillSourceDefinitionReader(),
            skillHosts,
            digestCalculator,
            skillManifestSerializer,
            new SkillManifest.Factory(skillHosts, new SkillManifestDigestCalculator(skillManifestSerializer)),
            new CanonicalSkillPackage.Factory(skillHosts, digestCalculator, skillManifestSerializer),
            new SkillBundleDigestCalculator(skillManifestSerializer),
            new CanonicalSkillBundle.Factory(new SkillBundleDigestCalculator(skillManifestSerializer)));
        var agentManifestSerializer = new AgentManifestJsonSerializer();
        var agentHosts = new AgentHostAdapterSet();
        var agentGenerator = new AgentPackageGenerationService(agentHosts, agentManifestSerializer, new AgentManifestDigestCalculator(agentManifestSerializer), digestCalculator);
        var mixedDigest = new AgentSkillsBundleDigestCalculator(skillManifestSerializer, agentManifestSerializer, digestCalculator);
        var agentReader = new CanonicalAgentPackageReader(
            agentManifestSerializer,
            digestCalculator,
            new AgentManifestDigestCalculator(agentManifestSerializer));
        var skillReader = new CanonicalSkillPackageReader(skillManifestSerializer, new SkillManifest.Factory(skillHosts, new SkillManifestDigestCalculator(skillManifestSerializer)), new CanonicalSkillPackage.Factory(skillHosts, digestCalculator, skillManifestSerializer));
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
                new AgentSourceDefinitionReader(agentHosts),
                skillGenerator,
                agentGenerator,
                mixedDigest),
            mixedBundleReader,
            new AgentSkillsBundleBuildPublisher(mixedBundleWriter, mixedSerializer, fileSystem));
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

        var fullBundleRoot = Path.GetFullPath(bundleRoot);
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
        var generatedRoot = Path.Combine(fullBundleRoot, "generated");
        var currentResult = Directory.Exists(generatedRoot) ? await bundleReader.ReadAsync(generatedRoot, cancellationToken).ConfigureAwait(false) : null;
        if (currentResult is not null && !currentResult.IsSuccess)
        {
            return SkillOperationResult<AgentSkillsBundleBuildResult>.FailureResult(currentResult.Failure!.Code, currentResult.Failure.Message);
        }

        var sourceChanged = target != source.BundleDefinition.BundleVersion;
        var current = currentResult?.Value;
        if (!sourceChanged && current is not null && current.Descriptor.BundleVersion == target && current.Descriptor.BundleDigest == candidate.Descriptor.BundleDigest)
        {
            return SkillOperationResult<AgentSkillsBundleBuildResult>.Success(new AgentSkillsBundleBuildResult(false, candidate.Descriptor));
        }

        if (check)
        {
            return SkillOperationResult<AgentSkillsBundleBuildResult>.FailureResult(SkillFailureCodes.BundleUpdateRequired, "Canonical v2 bundle requires generation.");
        }

        SkillOperationResult<string> write;
        if (sourceChanged)
        {
            var updated = new AgentSkillsBundleDefinition(
                source.BundleDefinition.SchemaVersion,
                source.BundleDefinition.CatalogId,
                target);
            write = await publisher.PublishSourceAndGeneratedAsync(
                    fullBundleRoot,
                    updated,
                    candidate,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            write = await publisher.PublishGeneratedAsync(candidate, generatedRoot, cancellationToken).ConfigureAwait(false);
        }

        if (!write.IsSuccess)
        {
            return SkillOperationResult<AgentSkillsBundleBuildResult>.FailureResult(write.Failure!.Code, write.Failure.Message);
        }

        return SkillOperationResult<AgentSkillsBundleBuildResult>.Success(new AgentSkillsBundleBuildResult(true, candidate.Descriptor));
    }
}

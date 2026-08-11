using MackySoft.AgentDistribution.Agents.Generation;
using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Agents.Packaging;
using MackySoft.AgentDistribution.Agents.Sources;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Generation;
using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Reconciles generated output from a source schema v3 bundle. </summary>
public sealed class AgentDistributionBundleBuildService
{
    private readonly AgentDistributionBundleGenerationService generationService;
    private readonly CanonicalAgentDistributionBundleReader bundleReader;
    private readonly CanonicalAgentDistributionBundleWriter bundleWriter;

    private AgentDistributionBundleBuildService (
        AgentDistributionBundleGenerationService generationService,
        CanonicalAgentDistributionBundleReader bundleReader,
        CanonicalAgentDistributionBundleWriter bundleWriter)
    {
        this.generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
        this.bundleWriter = bundleWriter ?? throw new ArgumentNullException(nameof(bundleWriter));
    }

    /// <summary> Creates the default v3 build service with all built-in host modules. </summary>
    public static AgentDistributionBundleBuildService CreateDefault ()
    {
        var skillManifestSerializer = new SkillManifestJsonSerializer();
        var digestCalculator = new PackageContentDigestCalculator();
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
            mixedBundleWriter);
    }

    /// <summary> Builds v3 generated output while preserving the authored bundle version. </summary>
    /// <param name="bundleRoot"> The root containing the source and generated bundle. </param>
    /// <param name="check"> Whether to fail without writing when reconciliation would change files. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through source access and publication. </param>
    /// <returns> The resulting descriptor and whether files changed, or a structured failure. </returns>
    public ValueTask<AgentDistributionOperationResult<AgentDistributionBundleBuildResult>> BuildAsync (
        string bundleRoot,
        bool check = false,
        CancellationToken cancellationToken = default)
    {
        return ReconcileAsync(bundleRoot, check, cancellationToken);
    }

    private async ValueTask<AgentDistributionOperationResult<AgentDistributionBundleBuildResult>> ReconcileAsync (
        string bundleRoot,
        bool check,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var fullBundleRoot = AbsolutePath.Parse(Path.GetFullPath(bundleRoot));
        var sourceResult = await generationService.ReadSourceAsync(fullBundleRoot, cancellationToken).ConfigureAwait(false);
        if (!sourceResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.FailureResult(sourceResult.Failure!.Code, sourceResult.Failure.Message);
        }

        var source = sourceResult.Value!;
        var authoredVersion = source.BundleDefinition.BundleVersion;

        CanonicalAgentDistributionBundle candidate;
        try
        {
            candidate = generationService.Generate(source, authoredVersion);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.FailureResult(
                AgentDistributionFailureCodes.SourceInvalid,
                $"The v3 source bundle could not be generated: {exception.Message}");
        }
        var generatedRoot = ContainedPath.Create(fullBundleRoot, RootRelativePath.Parse("generated")).Target;
        if (!FileSystemEntryInspector.TryInspect(generatedRoot, out var generatedRootObservation, out _))
        {
            return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"Generated v3 bundle output could not be inspected: {generatedRoot}");
        }

        CanonicalAgentDistributionBundle? current = null;
        if (generatedRootObservation.State == FileSystemEntryState.Directory)
        {
            var currentResult = await bundleReader.ReadAsync(generatedRoot, cancellationToken).ConfigureAwait(false);
            if (!currentResult.IsSuccess)
            {
                return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.FailureResult(currentResult.Failure!.Code, currentResult.Failure.Message);
            }

            current = currentResult.Value!;
        }
        else if (generatedRootObservation.State != FileSystemEntryState.Missing)
        {
            return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"Generated v3 bundle output must be a regular directory: {generatedRoot}");
        }

        if (current is not null && current.Descriptor.BundleVersion == authoredVersion && current.Descriptor.BundleDigest == candidate.Descriptor.BundleDigest)
        {
            return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.Success(new AgentDistributionBundleBuildResult(false, candidate.Descriptor));
        }

        if (check)
        {
            return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.FailureResult(AgentDistributionFailureCodes.BundleUpdateRequired, "Canonical v3 bundle requires generation.");
        }

        var write = await bundleWriter.WriteAsync(candidate, generatedRoot, cancellationToken).ConfigureAwait(false);

        if (!write.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.FailureResult(write.Failure!.Code, write.Failure.Message);
        }

        return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.Success(new AgentDistributionBundleBuildResult(true, candidate.Descriptor));
    }
}

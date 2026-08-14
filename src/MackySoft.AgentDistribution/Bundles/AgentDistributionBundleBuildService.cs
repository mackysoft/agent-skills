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

/// <summary> Reconciles generated v3 output from a source schema v4 bundle. </summary>
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

    /// <summary> Creates the default v4 source build service with all built-in host modules. </summary>
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

    /// <summary> Builds v3 generated output from one v4 source root while preserving the authored bundle version. </summary>
    /// <param name="sourceRoot"> The root containing source <c>bundle.json</c>, <c>skills</c>, and <c>agents</c> entries. </param>
    /// <param name="outputRoot"> The separate canonical output directory named <c>agent-distribution</c>. </param>
    /// <param name="check"> Whether to fail without writing when reconciliation would change files. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through source access and publication. </param>
    /// <returns> The resulting descriptor and whether files changed, or a structured failure. </returns>
    public ValueTask<AgentDistributionOperationResult<AgentDistributionBundleBuildResult>> BuildAsync (
        AbsolutePath sourceRoot,
        AbsolutePath outputRoot,
        bool check = false,
        CancellationToken cancellationToken = default)
    {
        return ReconcileAsync(sourceRoot, outputRoot, check, cancellationToken);
    }

    private async ValueTask<AgentDistributionOperationResult<AgentDistributionBundleBuildResult>> ReconcileAsync (
        AbsolutePath sourceRoot,
        AbsolutePath outputRoot,
        bool check,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceRoot);
        ArgumentNullException.ThrowIfNull(outputRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceRootResult = BundleBuildPathGuard.ValidateSourceRoot(sourceRoot);
        if (!sourceRootResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.FailureResult(sourceRootResult.Failure!.Code, sourceRootResult.Failure.Message);
        }

        var outputRootResult = BundleBuildPathGuard.ValidateOutputRoot(outputRoot);
        if (!outputRootResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.FailureResult(outputRootResult.Failure!.Code, outputRootResult.Failure.Message);
        }

        var distinctRootsResult = BundleBuildPathGuard.ValidateDistinctRoots(sourceRoot, outputRoot);
        if (!distinctRootsResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.FailureResult(distinctRootsResult.Failure!.Code, distinctRootsResult.Failure.Message);
        }

        var sourceResult = await generationService.ReadSourceAsync(sourceRoot, cancellationToken).ConfigureAwait(false);
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
                $"The v4 source bundle could not be generated: {exception.Message}");
        }

        CanonicalAgentDistributionBundle? current = null;
        if (Directory.Exists(outputRoot.Value))
        {
            var currentResult = await bundleReader.ReadAsync(outputRoot, cancellationToken).ConfigureAwait(false);
            if (!currentResult.IsSuccess)
            {
                return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.FailureResult(currentResult.Failure!.Code, currentResult.Failure.Message);
            }

            current = currentResult.Value!;
        }
        if (current is not null && current.Descriptor.BundleVersion == authoredVersion && current.Descriptor.BundleDigest == candidate.Descriptor.BundleDigest)
        {
            return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.Success(new AgentDistributionBundleBuildResult(false, candidate.Descriptor));
        }

        if (check)
        {
            return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.FailureResult(AgentDistributionFailureCodes.BundleUpdateRequired, "Canonical v3 bundle requires generation.");
        }

        var write = await bundleWriter.WriteAsync(candidate, outputRoot, cancellationToken).ConfigureAwait(false);

        if (!write.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.FailureResult(write.Failure!.Code, write.Failure.Message);
        }

        return AgentDistributionOperationResult<AgentDistributionBundleBuildResult>.Success(new AgentDistributionBundleBuildResult(true, candidate.Descriptor));
    }
}

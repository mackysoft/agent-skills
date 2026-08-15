using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Bundles;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Skills.Generation;

/// <summary> Reconciles generated output from a fixed-layout source bundle. </summary>
public sealed class SkillBundleBuildService
{
    private readonly SkillPackageGenerationService generationService;
    private readonly CanonicalSkillBundleReader bundleReader;
    private readonly CanonicalSkillBundleWriter bundleWriter;

    /// <summary> Initializes one bundle build service. </summary>
    /// <param name="generationService"> The canonical bundle generation service. </param>
    /// <param name="bundleReader"> The generated bundle reader and integrity boundary. </param>
    /// <param name="bundleWriter"> The generated bundle writer. </param>
    public SkillBundleBuildService (
        SkillPackageGenerationService generationService,
        CanonicalSkillBundleReader bundleReader,
        CanonicalSkillBundleWriter bundleWriter)
    {
        this.generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
        this.bundleWriter = bundleWriter ?? throw new ArgumentNullException(nameof(bundleWriter));
    }

    /// <summary> Reconciles generated output while preserving the authored bundle version. </summary>
    /// <param name="sourceRoot"> The root containing the v1 <c>bundle.json</c> and <c>definitions</c> source layout. </param>
    /// <param name="outputRoot"> The separate canonical output directory named <c>agent-distribution</c>. </param>
    /// <param name="check"> Whether to fail without writing when reconciliation would change files. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through source access and publication. </param>
    /// <returns> The resulting descriptor and whether files changed, or a structured source or generated failure. </returns>
    public ValueTask<AgentDistributionOperationResult<SkillBundleBuildResult>> BuildAsync (
        AbsolutePath sourceRoot,
        AbsolutePath outputRoot,
        bool check = false,
        CancellationToken cancellationToken = default)
    {
        return ReconcileAsync(sourceRoot, outputRoot, check, cancellationToken);
    }

    private async ValueTask<AgentDistributionOperationResult<SkillBundleBuildResult>> ReconcileAsync (
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
            return AgentDistributionOperationResult<SkillBundleBuildResult>.FailureResult(sourceRootResult.Failure!.Code, sourceRootResult.Failure.Message);
        }

        var outputRootResult = BundleBuildPathGuard.ValidateOutputRoot(outputRoot);
        if (!outputRootResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillBundleBuildResult>.FailureResult(outputRootResult.Failure!.Code, outputRootResult.Failure.Message);
        }

        var distinctRootsResult = BundleBuildPathGuard.ValidateDistinctRoots(sourceRoot, outputRoot);
        if (!distinctRootsResult.IsSuccess)
        {
            return AgentDistributionOperationResult<SkillBundleBuildResult>.FailureResult(distinctRootsResult.Failure!.Code, distinctRootsResult.Failure.Message);
        }

        var sourceResult = await generationService.ReadSourceAsync(sourceRoot, cancellationToken).ConfigureAwait(false);
        if (!sourceResult.IsSuccess)
        {
            return BuildFailure(sourceResult.Failure!);
        }

        var source = sourceResult.Value!;
        var authoredVersion = source.BundleDefinition.SkillBundleVersion;
        var candidate = generationService.GenerateAll(source, authoredVersion);
        CanonicalSkillBundle? generatedBundle = null;

        if (Directory.Exists(outputRoot.Value))
        {
            var generatedResult = await bundleReader.ReadAsync(outputRoot, cancellationToken).ConfigureAwait(false);
            if (!generatedResult.IsSuccess)
            {
                return BuildFailure(generatedResult.Failure!);
            }

            generatedBundle = generatedResult.Value!;
        }
        var generatedIsCurrent = generatedBundle is not null
            && generatedBundle.Descriptor.SkillBundleVersion == authoredVersion
            && generatedBundle.Descriptor.BundleDigest == candidate.Descriptor.BundleDigest;
        if (generatedIsCurrent)
        {
            return AgentDistributionOperationResult<SkillBundleBuildResult>.Success(
                new SkillBundleBuildResult(changed: false, candidate.Descriptor));
        }

        if (check)
        {
            return AgentDistributionOperationResult<SkillBundleBuildResult>.FailureResult(
                AgentDistributionFailureCodes.BundleUpdateRequired,
                $"Canonical SKILL bundle requires generation at version {authoredVersion}: {sourceRoot}");
        }

        var publicationResult = await bundleWriter.WriteAsync(
                candidate,
                outputRoot,
                cancellationToken)
            .ConfigureAwait(false);

        if (!publicationResult.IsSuccess)
        {
            return BuildFailure(publicationResult.Failure!);
        }

        return AgentDistributionOperationResult<SkillBundleBuildResult>.Success(
            new SkillBundleBuildResult(changed: true, candidate.Descriptor));
    }

    private static AgentDistributionOperationResult<SkillBundleBuildResult> BuildFailure (AgentDistributionFailure failure)
    {
        return AgentDistributionOperationResult<SkillBundleBuildResult>.FailureResult(failure.Code, failure.Message);
    }
}

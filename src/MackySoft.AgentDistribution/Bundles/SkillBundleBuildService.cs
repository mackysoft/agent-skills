using MackySoft.AgentDistribution.Generation;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Bundles;

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
    /// <param name="bundleRoot"> The root containing <c>bundle.json</c>, <c>definitions</c>, and fixed <c>generated</c> output. </param>
    /// <param name="check"> Whether to fail without writing when reconciliation would change files. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through source access and publication. </param>
    /// <returns> The resulting descriptor and whether files changed, or a structured source or generated failure. </returns>
    public ValueTask<AgentDistributionOperationResult<SkillBundleBuildResult>> BuildAsync (
        string bundleRoot,
        bool check = false,
        CancellationToken cancellationToken = default)
    {
        return ReconcileAsync(bundleRoot, check, cancellationToken);
    }

    private async ValueTask<AgentDistributionOperationResult<SkillBundleBuildResult>> ReconcileAsync (
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
            return BuildFailure(sourceResult.Failure!);
        }

        var source = sourceResult.Value!;
        var authoredVersion = source.BundleDefinition.SkillBundleVersion;
        var candidate = generationService.GenerateAll(source, authoredVersion);
        var generatedRoot = ContainedPath.Create(fullBundleRoot, RootRelativePath.Parse("generated")).Target;
        CanonicalSkillBundle? generatedBundle = null;

        if (!FileSystemEntryInspector.TryInspect(
                generatedRoot,
                out var generatedRootObservation,
                out _))
        {
            return AgentDistributionOperationResult<SkillBundleBuildResult>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"Generated SKILL bundle output could not be inspected: {generatedRoot}");
        }

        if (generatedRootObservation.State == FileSystemEntryState.Directory)
        {
            var generatedResult = await bundleReader.ReadAsync(generatedRoot, cancellationToken).ConfigureAwait(false);
            if (!generatedResult.IsSuccess)
            {
                return BuildFailure(generatedResult.Failure!);
            }

            generatedBundle = generatedResult.Value!;
        }
        else if (generatedRootObservation.State != FileSystemEntryState.Missing)
        {
            return AgentDistributionOperationResult<SkillBundleBuildResult>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"Generated SKILL bundle output must be a regular directory: {generatedRoot}");
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
                $"Canonical SKILL bundle requires generation at version {authoredVersion}: {fullBundleRoot}");
        }

        var publicationResult = await bundleWriter.WriteAsync(
                candidate,
                generatedRoot,
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

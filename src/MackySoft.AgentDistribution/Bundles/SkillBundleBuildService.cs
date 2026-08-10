using MackySoft.AgentDistribution.Generation;
using MackySoft.AgentDistribution.Serialization;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Reconciles a fixed-layout source bundle and prepares explicit release revisions. </summary>
public sealed class SkillBundleBuildService
{
    private readonly SkillPackageGenerationService generationService;
    private readonly CanonicalSkillBundleReader bundleReader;
    private readonly CanonicalSkillBundleWriter bundleWriter;
    private readonly SkillBundleJsonSerializer bundleSerializer;
    private readonly SourceAndGeneratedBundleTransaction transaction;

    /// <summary> Initializes one bundle build service. </summary>
    /// <param name="generationService"> The canonical bundle generation service. </param>
    /// <param name="bundleReader"> The generated bundle reader and integrity boundary. </param>
    /// <param name="bundleWriter"> The generated bundle writer. </param>
    /// <param name="bundleSerializer"> The canonical source bundle serializer. </param>
    public SkillBundleBuildService (
        SkillPackageGenerationService generationService,
        CanonicalSkillBundleReader bundleReader,
        CanonicalSkillBundleWriter bundleWriter,
        SkillBundleJsonSerializer bundleSerializer)
    {
        this.generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
        this.bundleWriter = bundleWriter ?? throw new ArgumentNullException(nameof(bundleWriter));
        this.bundleSerializer = bundleSerializer ?? throw new ArgumentNullException(nameof(bundleSerializer));
        transaction = new SourceAndGeneratedBundleTransaction(CanonicalTextFilePublisher.PublishAsync);
    }

    /// <summary> Reconciles generated output while preserving the authored bundle version. </summary>
    /// <param name="bundleRoot"> The root containing <c>bundle.json</c>, <c>definitions</c>, and fixed <c>generated</c> output. </param>
    /// <param name="check"> Whether to fail without writing when reconciliation would change files. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through source access and publication. </param>
    /// <returns> The resulting descriptor and whether files changed, or a structured source or generated failure. </returns>
    public ValueTask<SkillOperationResult<SkillBundleBuildResult>> BuildAsync (
        string bundleRoot,
        bool check = false,
        CancellationToken cancellationToken = default)
    {
        return ReconcileAsync(bundleRoot, targetBundleVersion: null, check, cancellationToken);
    }

    /// <summary> Publishes the current or next exact release revision and its matching generated output. </summary>
    /// <param name="bundleRoot"> The root containing <c>bundle.json</c>, <c>definitions</c>, and fixed <c>generated</c> output. </param>
    /// <param name="bundleVersion"> The exact current or next release revision. </param>
    /// <param name="check"> Whether to fail without writing when release preparation would change files. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through source access and publication. </param>
    /// <returns> The resulting descriptor and whether files changed, or a structured source, generated, or version failure. </returns>
    public ValueTask<SkillOperationResult<SkillBundleBuildResult>> PrepareReleaseAsync (
        string bundleRoot,
        int bundleVersion,
        bool check = false,
        CancellationToken cancellationToken = default)
    {
        return ReconcileAsync(bundleRoot, bundleVersion, check, cancellationToken);
    }

    private async ValueTask<SkillOperationResult<SkillBundleBuildResult>> ReconcileAsync (
        string bundleRoot,
        int? targetBundleVersion,
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
        SkillBundleVersion targetVersion;
        if (targetBundleVersion is null)
        {
            targetVersion = authoredVersion;
        }
        else if (!SkillBundleVersion.TryCreate(targetBundleVersion.Value, out var requestedVersion))
        {
            return SkillOperationResult<SkillBundleBuildResult>.FailureResult(
                SkillFailureCodes.InputInvalid,
                $"skillBundleVersion must be a positive integer: {targetBundleVersion.Value}");
        }
        else
        {
            targetVersion = requestedVersion;
        }

        var targetVersionFailure = ValidateTargetVersion(authoredVersion, targetVersion);
        if (targetVersionFailure is not null)
        {
            return BuildFailure(targetVersionFailure);
        }

        var candidate = generationService.GenerateAll(source, targetVersion);
        var generatedRoot = ContainedPath.Create(fullBundleRoot, RootRelativePath.Parse("generated")).Target;
        CanonicalSkillBundle? generatedBundle = null;

        if (!FileSystemEntryInspector.TryInspect(
                generatedRoot,
                out var generatedRootObservation,
                out _))
        {
            return SkillOperationResult<SkillBundleBuildResult>.FailureResult(
                SkillFailureCodes.PathUnsafe,
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
            return SkillOperationResult<SkillBundleBuildResult>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Generated SKILL bundle output must be a regular directory: {generatedRoot}");
        }

        var updatesSourceDefinition = targetVersion != authoredVersion;
        var generatedIsCurrent = generatedBundle is not null
            && generatedBundle.Descriptor.SkillBundleVersion == targetVersion
            && generatedBundle.Descriptor.BundleDigest == candidate.Descriptor.BundleDigest;
        if (!updatesSourceDefinition && generatedIsCurrent)
        {
            return SkillOperationResult<SkillBundleBuildResult>.Success(
                new SkillBundleBuildResult(changed: false, candidate.Descriptor));
        }

        if (check)
        {
            return SkillOperationResult<SkillBundleBuildResult>.FailureResult(
                SkillFailureCodes.BundleUpdateRequired,
                $"Canonical SKILL bundle requires generation at version {targetVersion}: {fullBundleRoot}");
        }

        SkillOperationResult<AbsolutePath> publicationResult;
        if (updatesSourceDefinition)
        {
            var authoredBundle = source.BundleDefinition;
            var finalSourceDefinition = new SkillBundleDefinition(
                authoredBundle.SchemaVersion,
                authoredBundle.CatalogId,
                targetVersion);
            ValidatePublicationIdentity(finalSourceDefinition, candidate.Descriptor);
            publicationResult = await transaction.PublishAsync(
                    fullBundleRoot,
                    bundleSerializer.SerializeDefinition(finalSourceDefinition),
                    (outputRoot, token) => bundleWriter.WriteAsync(candidate, outputRoot, token),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            publicationResult = await bundleWriter.WriteAsync(
                    candidate,
                    generatedRoot,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!publicationResult.IsSuccess)
        {
            return BuildFailure(publicationResult.Failure!);
        }

        return SkillOperationResult<SkillBundleBuildResult>.Success(
            new SkillBundleBuildResult(changed: true, candidate.Descriptor));
    }

    private static SkillFailure? ValidateTargetVersion (
        SkillBundleVersion authoredVersion,
        SkillBundleVersion targetVersion)
    {
        if (targetVersion.CompareTo(authoredVersion) < 0)
        {
            return SkillFailure.Create(
                SkillFailureCodes.InputInvalid,
                $"Requested skillBundleVersion {targetVersion} cannot be lower than the authored version {authoredVersion}.");
        }

        if (targetVersion != authoredVersion
            && targetVersion != authoredVersion.Next())
        {
            return SkillFailure.Create(
                SkillFailureCodes.InputInvalid,
                $"Requested skillBundleVersion must equal the authored version {authoredVersion} or its next revision.");
        }

        return null;
    }

    private static SkillOperationResult<SkillBundleBuildResult> BuildFailure (SkillFailure failure)
    {
        return SkillOperationResult<SkillBundleBuildResult>.FailureResult(failure.Code, failure.Message);
    }

    private static void ValidatePublicationIdentity (
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

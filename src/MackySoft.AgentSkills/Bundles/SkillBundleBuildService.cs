using MackySoft.AgentSkills.Generation;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Reconciles a fixed-layout source bundle and its canonical generated output. </summary>
public sealed class SkillBundleBuildService
{
    private readonly SkillPackageGenerationService generationService;
    private readonly CanonicalSkillBundleReader bundleReader;
    private readonly SkillBundleBuildPublisher publisher;

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
        : this(
            generationService,
            bundleReader,
            new SkillBundleBuildPublisher(
                bundleWriter ?? throw new ArgumentNullException(nameof(bundleWriter)),
                bundleSerializer ?? throw new ArgumentNullException(nameof(bundleSerializer)),
                new SkillBundleBuildFileSystem()))
    {
    }

    /// <summary> Initializes one bundle build service with its publication boundary. </summary>
    internal SkillBundleBuildService (
        SkillPackageGenerationService generationService,
        CanonicalSkillBundleReader bundleReader,
        SkillBundleBuildPublisher publisher)
    {
        this.generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    /// <summary> Reconciles generated output and advances the source bundle version exactly when content changed. </summary>
    /// <param name="bundleRoot"> The root containing <c>bundle.json</c>, <c>definitions</c>, and fixed <c>generated</c> output. </param>
    /// <param name="check"> Whether to fail without writing when reconciliation would change files. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through source access and publication. </param>
    /// <returns> The resulting descriptor and whether files changed, or a structured source, generated, or version failure. </returns>
    public async ValueTask<SkillOperationResult<SkillBundleBuildResult>> BuildAsync (
        string bundleRoot,
        bool check = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var fullBundleRoot = Path.GetFullPath(bundleRoot);
        var sourceResult = await generationService.ReadSourceAsync(fullBundleRoot, cancellationToken).ConfigureAwait(false);
        if (!sourceResult.IsSuccess)
        {
            return BuildFailure(sourceResult.Failure!);
        }

        var source = sourceResult.Value!;
        var authoredVersion = source.BundleDefinition.SkillBundleVersion;
        var candidate = generationService.GenerateAll(source, authoredVersion);
        var generatedRoot = Path.Combine(fullBundleRoot, "generated");
        CanonicalSkillBundle? generatedBundle = null;

        if (File.Exists(generatedRoot))
        {
            return SkillOperationResult<SkillBundleBuildResult>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Generated SKILL bundle output must be a directory: {generatedRoot}");
        }

        if (Directory.Exists(generatedRoot))
        {
            if (!SkillPackageFileSystemEntryGuard.IsDirectory(generatedRoot))
            {
                return SkillOperationResult<SkillBundleBuildResult>.FailureResult(
                    SkillFailureCodes.PathUnsafe,
                    $"Generated SKILL bundle output must be a regular directory: {generatedRoot}");
            }

            var generatedResult = await bundleReader.ReadAsync(generatedRoot, cancellationToken).ConfigureAwait(false);
            if (!generatedResult.IsSuccess)
            {
                return BuildFailure(generatedResult.Failure!);
            }

            generatedBundle = generatedResult.Value!;
        }

        var planResult = CreatePlan(candidate.Descriptor, generatedBundle?.Descriptor);
        if (!planResult.IsSuccess)
        {
            return BuildFailure(planResult.Failure!);
        }

        var plan = planResult.Value!;
        if (!plan.HasChanges)
        {
            return SkillOperationResult<SkillBundleBuildResult>.Success(
                new SkillBundleBuildResult(changed: false, candidate.Descriptor));
        }

        if (check)
        {
            return SkillOperationResult<SkillBundleBuildResult>.FailureResult(
                SkillFailureCodes.BundleUpdateRequired,
                $"Canonical SKILL bundle requires generation at version {plan.TargetSkillBundleVersion}: {fullBundleRoot}");
        }

        var finalBundle = plan.TargetSkillBundleVersion == authoredVersion
            ? candidate
            : generationService.GenerateAll(source, plan.TargetSkillBundleVersion);
        SkillOperationResult<string> publicationResult;
        if (plan.UpdatesSourceDefinition)
        {
            var authoredBundle = source.BundleDefinition;
            var finalSourceDefinition = new SkillBundleDefinition(
                authoredBundle.SchemaVersion,
                authoredBundle.CatalogId,
                plan.TargetSkillBundleVersion);
            publicationResult = await publisher.PublishSourceAndGeneratedAsync(
                    fullBundleRoot,
                    finalSourceDefinition,
                    finalBundle,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            publicationResult = await publisher.PublishGeneratedAsync(
                    finalBundle,
                    generatedRoot,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!publicationResult.IsSuccess)
        {
            return BuildFailure(publicationResult.Failure!);
        }

        return SkillOperationResult<SkillBundleBuildResult>.Success(
            new SkillBundleBuildResult(changed: true, finalBundle.Descriptor));
    }

    private static SkillOperationResult<SkillBundleBuildPlan> CreatePlan (
        SkillBundleDescriptor candidate,
        SkillBundleDescriptor? generated)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (generated is null)
        {
            return SkillOperationResult<SkillBundleBuildPlan>.Success(
                new SkillBundleBuildPlan(candidate.SkillBundleVersion, SkillBundleBuildChangeKind.Generated));
        }

        var authoredVersion = candidate.SkillBundleVersion;
        var generatedVersion = generated.SkillBundleVersion;
        var contentMatches = candidate.BundleDigest == generated.BundleDigest;

        if (authoredVersion == generatedVersion)
        {
            if (contentMatches)
            {
                return SkillOperationResult<SkillBundleBuildPlan>.Success(
                    new SkillBundleBuildPlan(authoredVersion, SkillBundleBuildChangeKind.None));
            }

            if (generatedVersion == int.MaxValue)
            {
                return VersionConflict(
                    authoredVersion,
                    generatedVersion,
                    "The generated version cannot be incremented because it is already Int32.MaxValue.");
            }

            return SkillOperationResult<SkillBundleBuildPlan>.Success(
                new SkillBundleBuildPlan(generatedVersion + 1, SkillBundleBuildChangeKind.SourceAndGenerated));
        }

        if (contentMatches)
        {
            return VersionConflict(
                authoredVersion,
                generatedVersion,
                "Bundle content is unchanged while source and generated versions differ.");
        }

        if (authoredVersion > 1 && generatedVersion == authoredVersion - 1)
        {
            return SkillOperationResult<SkillBundleBuildPlan>.Success(
                new SkillBundleBuildPlan(authoredVersion, SkillBundleBuildChangeKind.Generated));
        }

        return VersionConflict(
            authoredVersion,
            generatedVersion,
            "Changed bundle content requires the source version to equal the generated version or advance it by exactly one.");
    }

    private static SkillOperationResult<SkillBundleBuildPlan> VersionConflict (
        int authoredVersion,
        int generatedVersion,
        string reason)
    {
        return SkillOperationResult<SkillBundleBuildPlan>.FailureResult(
            SkillFailureCodes.BundleVersionConflict,
            $"SKILL bundle version conflict (source: {authoredVersion}, generated: {generatedVersion}). {reason}");
    }

    private static SkillOperationResult<SkillBundleBuildResult> BuildFailure (SkillFailure failure)
    {
        return SkillOperationResult<SkillBundleBuildResult>.FailureResult(failure.Code, failure.Message);
    }

    private enum SkillBundleBuildChangeKind
    {
        None = 0,
        Generated = 1,
        SourceAndGenerated = 2,
    }

    private sealed class SkillBundleBuildPlan
    {
        internal SkillBundleBuildPlan (
            int targetSkillBundleVersion,
            SkillBundleBuildChangeKind changeKind)
        {
            if (targetSkillBundleVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetSkillBundleVersion),
                    targetSkillBundleVersion,
                    "Target SKILL bundle version must be positive.");
            }

            if (!Enum.IsDefined(changeKind))
            {
                throw new ArgumentOutOfRangeException(nameof(changeKind), changeKind, "SKILL bundle build change kind is not supported.");
            }

            TargetSkillBundleVersion = targetSkillBundleVersion;
            ChangeKind = changeKind;
        }

        internal int TargetSkillBundleVersion { get; }

        internal SkillBundleBuildChangeKind ChangeKind { get; }

        internal bool HasChanges => ChangeKind != SkillBundleBuildChangeKind.None;

        internal bool UpdatesSourceDefinition => ChangeKind == SkillBundleBuildChangeKind.SourceAndGenerated;
    }
}

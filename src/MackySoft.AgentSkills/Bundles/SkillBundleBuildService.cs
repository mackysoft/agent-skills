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

    /// <summary> Reconciles generated output at the authored bundle version. </summary>
    /// <param name="bundleRoot"> The root containing <c>bundle.json</c>, <c>definitions</c>, and fixed <c>generated</c> output. </param>
    /// <param name="check"> Whether to fail without writing when reconciliation would change files. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through source access and publication. </param>
    /// <returns> The resulting descriptor and whether files changed, or a structured source, generated, or version failure. </returns>
    public ValueTask<SkillOperationResult<SkillBundleBuildResult>> BuildAsync (
        string bundleRoot,
        bool check = false,
        CancellationToken cancellationToken = default)
    {
        return BuildAsync(bundleRoot, skillBundleVersion: null, check, cancellationToken);
    }

    /// <summary> Reconciles generated output at an explicitly selected bundle version. </summary>
    /// <param name="bundleRoot"> The root containing <c>bundle.json</c>, <c>definitions</c>, and fixed <c>generated</c> output. </param>
    /// <param name="skillBundleVersion"> The exact target bundle version, or <see langword="null" /> to preserve the authored version. </param>
    /// <param name="check"> Whether to fail without writing when reconciliation would change files. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through source access and publication. </param>
    /// <returns> The resulting descriptor and whether files changed, or a structured source, generated, or version failure. </returns>
    public async ValueTask<SkillOperationResult<SkillBundleBuildResult>> BuildAsync (
        string bundleRoot,
        int? skillBundleVersion,
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
        SkillBundleVersion targetVersion;
        if (skillBundleVersion is null)
        {
            targetVersion = authoredVersion;
        }
        else if (!SkillBundleVersion.TryCreate(skillBundleVersion.Value, out var requestedVersion))
        {
            return SkillOperationResult<SkillBundleBuildResult>.FailureResult(
                SkillFailureCodes.InputInvalid,
                $"skillBundleVersion must be a positive integer: {skillBundleVersion.Value}");
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

        SkillOperationResult<string> publicationResult;
        if (updatesSourceDefinition)
        {
            var authoredBundle = source.BundleDefinition;
            var finalSourceDefinition = new SkillBundleDefinition(
                authoredBundle.SchemaVersion,
                authoredBundle.CatalogId,
                targetVersion);
            publicationResult = await publisher.PublishSourceAndGeneratedAsync(
                    fullBundleRoot,
                    finalSourceDefinition,
                    candidate,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            publicationResult = await publisher.PublishGeneratedAsync(
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
}

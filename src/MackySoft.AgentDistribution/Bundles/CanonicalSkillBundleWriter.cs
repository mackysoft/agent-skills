using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Serialization;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Writes a validated generated bundle descriptor and package set to one output directory. </summary>
public sealed class CanonicalSkillBundleWriter
{
    private readonly CanonicalSkillPackageWriter packageWriter;
    private readonly SkillBundleJsonSerializer bundleSerializer;
    private readonly CanonicalSkillBundleReader bundleReader;

    /// <summary> Initializes a writer with package, descriptor, and bundle integrity contracts. </summary>
    /// <param name="packageWriter"> The canonical package writer. </param>
    /// <param name="bundleSerializer"> The canonical bundle descriptor serializer. </param>
    /// <param name="bundleReader"> The reader used to verify the complete staged bundle before publication. </param>
    public CanonicalSkillBundleWriter (
        CanonicalSkillPackageWriter packageWriter,
        SkillBundleJsonSerializer bundleSerializer,
        CanonicalSkillBundleReader bundleReader)
    {
        this.packageWriter = packageWriter ?? throw new ArgumentNullException(nameof(packageWriter));
        this.bundleSerializer = bundleSerializer ?? throw new ArgumentNullException(nameof(bundleSerializer));
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
    }

    /// <summary> Replaces an output root with one complete canonical bundle. </summary>
    /// <param name="bundle"> The generated bundle whose descriptor matches every package. </param>
    /// <param name="outputRoot"> A directory named <c>generated</c> or <c>skills</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through file access. </param>
    /// <returns> The full output root path, or a validation/path failure. </returns>
    internal async ValueTask<SkillOperationResult<AbsolutePath>> WriteAsync (
        CanonicalSkillBundle bundle,
        AbsolutePath outputRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(outputRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var outputRootResult = ResolveOutputRoot(outputRoot);
        if (!outputRootResult.IsSuccess)
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(outputRootResult.Failure!.Code, outputRootResult.Failure.Message);
        }

        var fullOutputRoot = outputRootResult.Value!;
        if (!fullOutputRoot.TryGetParent(out var parentDirectory))
        {
            throw new InvalidOperationException($"Generated SKILL output root parent could not be resolved: {fullOutputRoot.Value}");
        }

        Directory.CreateDirectory(parentDirectory.Value);

        var operationId = Guid.NewGuid().ToString("N");
        var outputName = Path.GetFileName(fullOutputRoot.Value);
        var stagingRoot = ContainedPath.Create(parentDirectory, RootRelativePath.Parse($".{outputName}.staging.{operationId}")).Target;
        var backupRoot = ContainedPath.Create(parentDirectory, RootRelativePath.Parse($".{outputName}.backup.{operationId}")).Target;
        var published = false;

        try
        {
            foreach (var package in bundle.Packages)
            {
                var packageWriteResult = await packageWriter.WriteToStagingAsync(
                        package,
                        stagingRoot,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!packageWriteResult.IsSuccess)
                {
                    return SkillOperationResult<AbsolutePath>.FailureResult(packageWriteResult.Failure!.Code, packageWriteResult.Failure.Message);
                }
            }

            var descriptorPathResult = PackagePathResolver.ResolveUnderRoot(
                stagingRoot,
                ContainedPath.Create(stagingRoot, RootRelativePath.Parse("bundle.json")).Target);
            if (!descriptorPathResult.IsSuccess)
            {
                return SkillOperationResult<AbsolutePath>.FailureResult(
                    descriptorPathResult.Failure!.Code,
                    descriptorPathResult.Failure.Message);
            }

            await CanonicalTextFilePublisher.PublishAsync(
                    descriptorPathResult.Value!,
                    bundleSerializer.SerializeDescriptor(bundle.Descriptor),
                    cancellationToken)
                .ConfigureAwait(false);

            var stagedBundleResult = await bundleReader.ReadAsync(stagingRoot, cancellationToken).ConfigureAwait(false);
            if (!stagedBundleResult.IsSuccess)
            {
                return SkillOperationResult<AbsolutePath>.FailureResult(
                    stagedBundleResult.Failure!.Code,
                    stagedBundleResult.Failure.Message);
            }

            cancellationToken.ThrowIfCancellationRequested();
            CanonicalSkillBundleDirectoryPublisher.Publish(stagingRoot, fullOutputRoot, backupRoot);
            published = true;
            TryDeleteDirectory(backupRoot);

            return SkillOperationResult<AbsolutePath>.Success(fullOutputRoot);
        }
        finally
        {
            if (!published)
            {
                TryDeleteDirectory(stagingRoot);
            }
        }
    }

    private static SkillOperationResult<AbsolutePath> ResolveOutputRoot (AbsolutePath outputRoot)
    {
        var outputName = Path.GetFileName(outputRoot.Value);
        if (!string.Equals(outputName, "generated", StringComparison.Ordinal)
            && !string.Equals(outputName, "skills", StringComparison.Ordinal))
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Generated SKILL output root must be named 'generated' or 'skills': {outputRoot}");
        }

        if (!FileSystemEntryInspector.TryInspect(
                outputRoot,
                out var outputRootObservation,
                out _)
            || outputRootObservation.State is not FileSystemEntryState.Missing and not FileSystemEntryState.Directory)
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Generated SKILL output root must be a regular directory: {outputRoot}");
        }

        return SkillOperationResult<AbsolutePath>.Success(outputRoot);
    }

    private static void TryDeleteDirectory (AbsolutePath path)
    {
        try
        {
            if (Directory.Exists(path.Value))
            {
                Directory.Delete(path.Value, recursive: true);
            }
        }
        catch (IOException)
        {
            // Cleanup after publication or failure is best effort; the committed output remains authoritative.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup after publication or failure is best effort; the committed output remains authoritative.
        }
    }
}

using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Bundles;

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
    internal async ValueTask<SkillOperationResult<string>> WriteAsync (
        CanonicalSkillBundle bundle,
        string outputRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var outputRootResult = ResolveOutputRoot(outputRoot);
        if (!outputRootResult.IsSuccess)
        {
            return outputRootResult;
        }

        var fullOutputRoot = outputRootResult.Value!;
        var parentDirectory = Path.GetDirectoryName(fullOutputRoot)
            ?? throw new InvalidOperationException($"Generated SKILL output root parent could not be resolved: {fullOutputRoot}");
        Directory.CreateDirectory(parentDirectory);

        var operationId = Guid.NewGuid().ToString("N");
        var outputName = Path.GetFileName(fullOutputRoot);
        var stagingRoot = Path.Combine(parentDirectory, $".{outputName}.staging.{operationId}");
        var backupRoot = Path.Combine(parentDirectory, $".{outputName}.backup.{operationId}");
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
                    return packageWriteResult;
                }
            }

            var descriptorPathResult = SkillPackagePathBoundary.ResolvePackageFilePath(stagingRoot, "bundle.json");
            if (!descriptorPathResult.IsSuccess)
            {
                return SkillOperationResult<string>.FailureResult(
                    descriptorPathResult.Failure!.Code,
                    descriptorPathResult.Failure.Message);
            }

            await SkillPackageFileWriter.WriteAllTextAtomicallyAsync(
                    descriptorPathResult.Value!,
                    bundleSerializer.SerializeDescriptor(bundle.Descriptor),
                    cancellationToken)
                .ConfigureAwait(false);

            var stagedBundleResult = await bundleReader.ReadAsync(stagingRoot, cancellationToken).ConfigureAwait(false);
            if (!stagedBundleResult.IsSuccess)
            {
                return SkillOperationResult<string>.FailureResult(
                    stagedBundleResult.Failure!.Code,
                    stagedBundleResult.Failure.Message);
            }

            cancellationToken.ThrowIfCancellationRequested();
            CanonicalSkillBundleDirectoryPublisher.Publish(stagingRoot, fullOutputRoot, backupRoot);
            published = true;
            TryDeleteDirectory(backupRoot);

            return SkillOperationResult<string>.Success(fullOutputRoot);
        }
        finally
        {
            if (!published)
            {
                TryDeleteDirectory(stagingRoot);
            }
        }
    }

    private static SkillOperationResult<string> ResolveOutputRoot (string outputRoot)
    {
        var fullOutputRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputRoot));
        var outputName = Path.GetFileName(fullOutputRoot);
        if (!string.Equals(outputName, "generated", StringComparison.Ordinal)
            && !string.Equals(outputName, "skills", StringComparison.Ordinal))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Generated SKILL output root must be named 'generated' or 'skills': {fullOutputRoot}");
        }

        if (File.Exists(fullOutputRoot)
            || (Directory.Exists(fullOutputRoot) && !SkillPackageFileSystemEntryGuard.IsDirectory(fullOutputRoot)))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Generated SKILL output root must be a regular directory: {fullOutputRoot}");
        }

        return SkillOperationResult<string>.Success(fullOutputRoot);
    }

    private static void TryDeleteDirectory (string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
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

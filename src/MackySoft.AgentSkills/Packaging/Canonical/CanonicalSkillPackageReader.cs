using MackySoft.AgentSkills.Manifests;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Packaging.Canonical;

/// <summary> Reads generated canonical SKILL packages from a <c>skills</c> directory. </summary>
public sealed class CanonicalSkillPackageReader
{
    private readonly SkillManifestJsonSerializer manifestSerializer;
    private readonly SkillManifest.Factory manifestFactory;
    private readonly CanonicalSkillPackage.Factory packageFactory;

    /// <summary> Initializes a new instance of the <see cref="CanonicalSkillPackageReader" /> class. </summary>
    /// <param name="manifestSerializer"> The manifest serializer. </param>
    /// <param name="manifestFactory"> The canonical manifest construction boundary. </param>
    /// <param name="packageFactory"> The canonical package construction boundary. </param>
    public CanonicalSkillPackageReader (
        SkillManifestJsonSerializer manifestSerializer,
        SkillManifest.Factory manifestFactory,
        CanonicalSkillPackage.Factory packageFactory)
    {
        this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
        this.manifestFactory = manifestFactory ?? throw new ArgumentNullException(nameof(manifestFactory));
        this.packageFactory = packageFactory ?? throw new ArgumentNullException(nameof(packageFactory));
    }

    /// <summary> Reads all generated canonical SKILL packages under a package root. </summary>
    /// <param name="packageRoot"> The generated <c>skills</c> directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The canonical packages or validation failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> ReadAllAsync (
        string packageRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(packageRoot))
        {
            return Failure($"Generated skills directory does not exist: {packageRoot}");
        }

        var fullPackageRoot = Path.GetFullPath(packageRoot);
        if (!SkillPackageFileSystemEntryGuard.IsDirectory(fullPackageRoot))
        {
            return Failure($"Generated skills root must be a regular directory: {fullPackageRoot}");
        }

        var packages = new List<CanonicalSkillPackage>();
        foreach (var skillDirectory in Directory.GetDirectories(fullPackageRoot).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SkillPackageFileSystemEntryGuard.IsDirectory(skillDirectory))
            {
                return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                    SkillFailureCodes.PathUnsafe,
                    $"Generated skills root contains an unsupported non-regular package directory: {Path.GetFileName(skillDirectory)}");
            }

            var result = await ReadOneAsync(fullPackageRoot, skillDirectory, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                    result.Failure!.Code,
                    result.Failure.Message);
            }

            packages.Add(result.Value!);
        }

        if (packages.Count == 0)
        {
            return Failure($"Generated skills directory does not contain any packages: {fullPackageRoot}");
        }

        return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.Success(packages
            .OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal)
            .ToArray());
    }

    private async ValueTask<SkillOperationResult<CanonicalSkillPackage>> ReadOneAsync (
        string packageRoot,
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var directoryResult = SkillPackagePathBoundary.ResolveUnderRoot(packageRoot, skillDirectory);
        if (!directoryResult.IsSuccess)
        {
            return SkillOperationResult<CanonicalSkillPackage>.FailureResult(directoryResult.Failure!.Code, directoryResult.Failure.Message);
        }

        var filesResult = await ReadFilesAsync(directoryResult.Value!, cancellationToken).ConfigureAwait(false);
        if (!filesResult.IsSuccess)
        {
            return SkillOperationResult<CanonicalSkillPackage>.FailureResult(filesResult.Failure!.Code, filesResult.Failure.Message);
        }

        var files = filesResult.Value!;
        var manifestFile = files.SingleOrDefault(static file => string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal));
        if (manifestFile is null)
        {
            return PackageFailure("Generated SKILL package is missing agent-skill.json.");
        }

        var manifestText = manifestFile.Content;
        var manifestResult = manifestSerializer.TryDeserialize(manifestText);
        if (!manifestResult.IsSuccess)
        {
            return PackageFailure(manifestResult.Failure!.Message);
        }

        var canonicalManifestResult = manifestFactory.CreateCanonical(manifestResult.Value!);
        if (!canonicalManifestResult.IsSuccess)
        {
            return PackageFailure(canonicalManifestResult.Failure!.Message);
        }

        var manifest = canonicalManifestResult.Value!;
        if (!string.Equals(Path.GetFileName(directoryResult.Value!), manifest.SkillName.Value, StringComparison.Ordinal))
        {
            return PackageFailure($"agent-skill.json skillName must match generated package directory name: {manifest.SkillName}");
        }

        var packageFiles = files
            .Select(file => string.Equals(file.RelativePath, "agent-skill.json", StringComparison.Ordinal)
                ? new SkillPackageFile(file.RelativePath, manifestText)
                : file)
            .ToArray();
        return packageFactory.CreateCanonical(new CanonicalSkillPackageCandidate(manifest, packageFiles));
    }

    private async ValueTask<SkillOperationResult<IReadOnlyList<SkillPackageFile>>> ReadFilesAsync (
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var files = new List<SkillPackageFile>();
        var readResult = await ReadDirectoryEntriesAsync(skillDirectory, skillDirectory, files, cancellationToken).ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<SkillPackageFile>>.FailureResult(
                readResult.Failure!.Code,
                readResult.Failure.Message);
        }

        return SkillOperationResult<IReadOnlyList<SkillPackageFile>>.Success(files
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
            .ToArray());
    }

    private async ValueTask<SkillOperationResult<bool>> ReadDirectoryEntriesAsync (
        string skillDirectory,
        string directoryPath,
        List<SkillPackageFile> files,
        CancellationToken cancellationToken)
    {
        foreach (var entryPath in Directory.EnumerateFileSystemEntries(directoryPath).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(skillDirectory, entryPath).Replace(Path.DirectorySeparatorChar, '/');
            if (!SkillRelativePath.IsSafeFilePath(relativePath))
            {
                return BoolFailure(
                    $"Generated SKILL package contains an unsafe path: {relativePath}");
            }

            if (Directory.Exists(entryPath))
            {
                if (!SkillPackageFileSystemEntryGuard.IsDirectory(entryPath))
                {
                    return BoolFailure(
                        $"Generated SKILL package contains an unsupported non-regular directory: {relativePath}");
                }

                var directoryResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, entryPath);
                if (!directoryResult.IsSuccess)
                {
                    return SkillOperationResult<bool>.FailureResult(directoryResult.Failure!.Code, directoryResult.Failure.Message);
                }

                var result = await ReadDirectoryEntriesAsync(skillDirectory, directoryResult.Value!, files, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return result;
                }

                continue;
            }

            if (!File.Exists(entryPath))
            {
                return BoolFailure(
                    $"Generated SKILL package contains an unsupported non-regular path: {relativePath}");
            }

            if (!SkillPackageFileSystemEntryGuard.IsRegularFile(entryPath))
            {
                return BoolFailure(
                    $"Generated SKILL package contains an unsupported non-regular file: {relativePath}");
            }

            var pathResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, entryPath);
            if (!pathResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
            }

            var contentResult = await SkillPackageTextFileReader.ReadAsync(pathResult.Value!, cancellationToken).ConfigureAwait(false);
            if (!contentResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(
                    contentResult.Failure!.Code,
                    contentResult.Failure.Message);
            }

            files.Add(new SkillPackageFile(relativePath, contentResult.Value!));
        }

        return SkillOperationResult<bool>.Success(true);
    }

    private static SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>> Failure (string message)
    {
        return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
    }

    private static SkillOperationResult<CanonicalSkillPackage> PackageFailure (string message)
    {
        return SkillOperationResult<CanonicalSkillPackage>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
    }

    private static SkillOperationResult<bool> BoolFailure (string message)
    {
        return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
    }
}

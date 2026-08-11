using MackySoft.AgentDistribution.Manifests;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Packaging.Canonical;

/// <summary> Reads generated canonical SKILL packages from a <c>skills</c> directory. </summary>
public sealed class CanonicalSkillPackageReader
{
    private static readonly PackageRelativePath ManifestPath = PackageRelativePath.Parse("agent-skill.json");

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
    public async ValueTask<AgentDistributionOperationResult<IReadOnlyList<CanonicalSkillPackage>>> ReadAllAsync (
        AbsolutePath packageRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPackageRoot = packageRoot;
        if (!FileSystemEntryInspector.TryInspect(
                fullPackageRoot,
                out var packageRootObservation,
                out _))
        {
            return AgentDistributionOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"Generated skills root could not be inspected: {fullPackageRoot.Value}");
        }

        if (packageRootObservation.State == FileSystemEntryState.Missing)
        {
            return Failure($"Generated skills directory does not exist: {packageRoot}");
        }

        if (packageRootObservation.State != FileSystemEntryState.Directory)
        {
            return Failure($"Generated skills root must be a regular directory: {fullPackageRoot.Value}");
        }

        var packages = new List<CanonicalSkillPackage>();
        foreach (var skillDirectoryText in Directory.GetDirectories(fullPackageRoot.Value).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var skillDirectory = AbsolutePath.Parse(skillDirectoryText);

            if (!FileSystemEntryInspector.TryInspect(
                    skillDirectory,
                    out var skillDirectoryObservation,
                    out _)
                || skillDirectoryObservation.State != FileSystemEntryState.Directory)
            {
                return AgentDistributionOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                    AgentDistributionFailureCodes.PathUnsafe,
                    $"Generated skills root contains an unsupported non-regular package directory: {Path.GetFileName(skillDirectory.Value)}");
            }

            var result = await ReadOneAsync(fullPackageRoot, skillDirectory, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return AgentDistributionOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                    result.Failure!.Code,
                    result.Failure.Message);
            }

            packages.Add(result.Value!);
        }

        if (packages.Count == 0)
        {
            return Failure($"Generated skills directory does not contain any packages: {fullPackageRoot.Value}");
        }

        return AgentDistributionOperationResult<IReadOnlyList<CanonicalSkillPackage>>.Success(packages
            .OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal)
            .ToArray());
    }

    private async ValueTask<AgentDistributionOperationResult<CanonicalSkillPackage>> ReadOneAsync (
        AbsolutePath packageRoot,
        AbsolutePath skillDirectory,
        CancellationToken cancellationToken)
    {
        var directoryResult = PackagePathResolver.ResolveUnderRoot(packageRoot, skillDirectory);
        if (!directoryResult.IsSuccess)
        {
            return AgentDistributionOperationResult<CanonicalSkillPackage>.FailureResult(directoryResult.Failure!.Code, directoryResult.Failure.Message);
        }

        var filesResult = await ReadFilesAsync(directoryResult.Value!, cancellationToken).ConfigureAwait(false);
        if (!filesResult.IsSuccess)
        {
            return AgentDistributionOperationResult<CanonicalSkillPackage>.FailureResult(filesResult.Failure!.Code, filesResult.Failure.Message);
        }

        var files = filesResult.Value!;
        var manifestFile = files.SingleOrDefault(static file => file.RelativePath == ManifestPath);
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
        if (!string.Equals(Path.GetFileName(directoryResult.Value!.Value), manifest.SkillName.Value, StringComparison.Ordinal))
        {
            return PackageFailure($"agent-skill.json skillName must match generated package directory name: {manifest.SkillName}");
        }

        var packageFiles = files
            .Select(file => file.RelativePath == ManifestPath
                ? new PackageTextFile(file.RelativePath, manifestText)
                : file)
            .ToArray();
        return packageFactory.CreateCanonical(new CanonicalSkillPackageCandidate(manifest, packageFiles));
    }

    private async ValueTask<AgentDistributionOperationResult<IReadOnlyList<PackageTextFile>>> ReadFilesAsync (
        AbsolutePath skillDirectory,
        CancellationToken cancellationToken)
    {
        var files = new List<PackageTextFile>();
        var readResult = await ReadDirectoryEntriesAsync(skillDirectory, skillDirectory, files, cancellationToken).ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return AgentDistributionOperationResult<IReadOnlyList<PackageTextFile>>.FailureResult(
                readResult.Failure!.Code,
                readResult.Failure.Message);
        }

        return AgentDistributionOperationResult<IReadOnlyList<PackageTextFile>>.Success(files
            .OrderBy(static file => file.RelativePath.Value, StringComparer.Ordinal)
            .ToArray());
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> ReadDirectoryEntriesAsync (
        AbsolutePath skillDirectory,
        AbsolutePath directoryPath,
        List<PackageTextFile> files,
        CancellationToken cancellationToken)
    {
        foreach (var entryPathText in Directory.EnumerateFileSystemEntries(directoryPath.Value).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entryPath = AbsolutePath.Parse(entryPathText);

            var relativePath = Path.GetRelativePath(skillDirectory.Value, entryPath.Value).Replace(Path.DirectorySeparatorChar, '/');
            if (!PackageRelativePath.TryParse(relativePath, out var packageRelativePath))
            {
                return BoolFailure(
                    $"Generated SKILL package contains an unsafe path: {relativePath}");
            }

            if (!FileSystemEntryInspector.TryInspect(
                    entryPath,
                    out var entryObservation,
                    out _))
            {
                return BoolFailure(
                    $"Generated SKILL package contains an unsupported non-regular path: {relativePath}");
            }

            if (entryObservation.State == FileSystemEntryState.Directory)
            {
                var directoryResult = PackagePathResolver.ResolveUnderRoot(skillDirectory, entryPath);
                if (!directoryResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<bool>.FailureResult(directoryResult.Failure!.Code, directoryResult.Failure.Message);
                }

                var result = await ReadDirectoryEntriesAsync(skillDirectory, directoryResult.Value!, files, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return result;
                }

                continue;
            }

            if (entryObservation.State != FileSystemEntryState.RegularFile)
            {
                return BoolFailure(
                    $"Generated SKILL package contains an unsupported non-regular path: {relativePath}");
            }

            var pathResult = PackagePathResolver.ResolveUnderRoot(skillDirectory, entryPath);
            if (!pathResult.IsSuccess)
            {
                return AgentDistributionOperationResult<bool>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
            }

            var contentResult = await CanonicalPackageTextReader.ReadAsync(pathResult.Value!, cancellationToken).ConfigureAwait(false);
            if (!contentResult.IsSuccess)
            {
                return AgentDistributionOperationResult<bool>.FailureResult(
                    contentResult.Failure!.Code,
                    contentResult.Failure.Message);
            }

            files.Add(new PackageTextFile(packageRelativePath, contentResult.Value!));
        }

        return AgentDistributionOperationResult<bool>.Success(true);
    }

    private static AgentDistributionOperationResult<IReadOnlyList<CanonicalSkillPackage>> Failure (string message)
    {
        return AgentDistributionOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(AgentDistributionFailureCodes.ManifestInvalid, message);
    }

    private static AgentDistributionOperationResult<CanonicalSkillPackage> PackageFailure (string message)
    {
        return AgentDistributionOperationResult<CanonicalSkillPackage>.FailureResult(AgentDistributionFailureCodes.ManifestInvalid, message);
    }

    private static AgentDistributionOperationResult<bool> BoolFailure (string message)
    {
        return AgentDistributionOperationResult<bool>.FailureResult(AgentDistributionFailureCodes.ManifestInvalid, message);
    }
}

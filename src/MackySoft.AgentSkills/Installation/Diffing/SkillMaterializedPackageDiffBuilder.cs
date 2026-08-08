using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Materialization;
using MackySoft.AgentSkills.Packaging.Paths;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Installation.Diffing;

/// <summary> Builds structured file diffs between an installed target and a materialized package. </summary>
public sealed class SkillMaterializedPackageDiffBuilder
{
    /// <summary> Builds one structured diff for a target directory and desired materialized package. </summary>
    /// <param name="skillDirectory"> The target skill directory. </param>
    /// <param name="materializedPackage"> The desired materialized package. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Structured diffs or a path-safety failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<SkillActionDiff>>> BuildAsync (
        AbsolutePath skillDirectory,
        SkillMaterializedPackage materializedPackage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillDirectory);
        ArgumentNullException.ThrowIfNull(materializedPackage);
        cancellationToken.ThrowIfCancellationRequested();

        var beforeResult = await ReadExistingTargetEntriesAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!beforeResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<SkillActionDiff>>.FailureResult(
                beforeResult.Failure!.Code,
                beforeResult.Failure.Message);
        }

        var beforeFiles = beforeResult.Value!.Files;
        var afterFiles = CreateNormalizedPackageFileMap(materializedPackage);

        return SkillOperationResult<IReadOnlyList<SkillActionDiff>>.Success(BuildDiffs(beforeFiles, afterFiles));
    }

    /// <summary> Builds structured diffs when requested, or returns an empty diff list. </summary>
    /// <param name="skillDirectory"> The target skill directory. </param>
    /// <param name="materializedPackage"> The desired materialized package. </param>
    /// <param name="printDiff"> Whether structured diffs should be included. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Structured diffs, an empty list, or a path-safety/read failure. </returns>
    public ValueTask<SkillOperationResult<IReadOnlyList<SkillActionDiff>>> BuildOptionalAsync (
        AbsolutePath skillDirectory,
        SkillMaterializedPackage materializedPackage,
        bool printDiff,
        CancellationToken cancellationToken = default)
    {
        return printDiff
            ? BuildAsync(skillDirectory, materializedPackage, cancellationToken)
            : ValueTask.FromResult(SkillOperationResult<IReadOnlyList<SkillActionDiff>>.Success(Array.Empty<SkillActionDiff>()));
    }

    /// <summary>
    /// Builds replacement file changes, a target snapshot, and optional structured diffs for one target directory.
    /// </summary>
    /// <param name="skillDirectory"> The target skill directory. </param>
    /// <param name="materializedPackage"> The desired materialized package. </param>
    /// <param name="printDiff"> Whether structured diffs should be included. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// Replacement file changes with the target snapshot and optional diffs, or a path-safety/read failure.
    /// File changes are returned even when <paramref name="printDiff" /> is <see langword="false" />.
    /// </returns>
    internal async ValueTask<SkillOperationResult<SkillMaterializedPackageChangePlan>> BuildReplacementPlanAsync (
        AbsolutePath skillDirectory,
        SkillMaterializedPackage materializedPackage,
        bool printDiff,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillDirectory);
        ArgumentNullException.ThrowIfNull(materializedPackage);
        cancellationToken.ThrowIfCancellationRequested();

        var beforeResult = await ReadExistingTargetEntriesAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!beforeResult.IsSuccess)
        {
            return SkillOperationResult<SkillMaterializedPackageChangePlan>.FailureResult(
                beforeResult.Failure!.Code,
                beforeResult.Failure.Message);
        }

        var beforeEntries = beforeResult.Value!;
        var beforeFiles = beforeEntries.Files;
        var afterFiles = CreateNormalizedPackageFileMap(materializedPackage);
        var diffs = printDiff ? BuildDiffs(beforeFiles, afterFiles) : Array.Empty<SkillActionDiff>();
        var fileChanges = new SkillActionFileChangePlan(
            BuildReplacementFileChanges(beforeFiles, afterFiles),
            CreateTargetSnapshot(beforeEntries));

        return SkillOperationResult<SkillMaterializedPackageChangePlan>.Success(new SkillMaterializedPackageChangePlan(
            diffs,
            fileChanges));
    }

    /// <summary> Builds deterministic file removals and a target snapshot for deleting one target directory. </summary>
    /// <param name="skillDirectory"> The target skill directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// Removal file changes for existing files with the target snapshot, or a path-safety/read failure.
    /// Directories are represented only in the target snapshot.
    /// </returns>
    internal async ValueTask<SkillOperationResult<SkillActionFileChangePlan>> BuildDeletionFileChangesAsync (
        AbsolutePath skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var beforeResult = await ReadExistingTargetEntriesAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!beforeResult.IsSuccess)
        {
            return SkillOperationResult<SkillActionFileChangePlan>.FailureResult(
                beforeResult.Failure!.Code,
                beforeResult.Failure.Message);
        }

        var beforeEntries = beforeResult.Value!;
        var beforeFiles = beforeEntries.Files;
        return SkillOperationResult<SkillActionFileChangePlan>.Success(new SkillActionFileChangePlan(
            new SkillActionFileChanges(
                Array.Empty<PackageRelativePath>(),
                beforeFiles.Keys.OrderBy(static path => path.Value, StringComparer.Ordinal).ToArray()),
            CreateTargetSnapshot(beforeEntries)));
    }

    /// <summary> Builds the current target snapshot used by execution preconditions. </summary>
    /// <param name="skillDirectory"> The target skill directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// The current file-and-directory target snapshot, an empty snapshot when the target is missing, or a
    /// path-safety/read failure.
    /// </returns>
    internal async ValueTask<SkillOperationResult<SkillActionTargetSnapshot>> BuildTargetSnapshotAsync (
        AbsolutePath skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var beforeResult = await ReadExistingTargetEntriesAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!beforeResult.IsSuccess)
        {
            return SkillOperationResult<SkillActionTargetSnapshot>.FailureResult(
                beforeResult.Failure!.Code,
                beforeResult.Failure.Message);
        }

        return SkillOperationResult<SkillActionTargetSnapshot>.Success(CreateTargetSnapshot(beforeResult.Value!));
    }

    private static IReadOnlyList<SkillActionDiff> BuildDiffs (
        IReadOnlyDictionary<PackageRelativePath, string> beforeFiles,
        IReadOnlyDictionary<PackageRelativePath, string> afterFiles)
    {
        var relativePaths = beforeFiles.Keys
            .Concat(afterFiles.Keys)
            .Distinct()
            .OrderBy(static path => path.Value, StringComparer.Ordinal);

        var fileDiffs = new List<SkillFileDiff>();
        foreach (var relativePath in relativePaths)
        {
            var hasBefore = beforeFiles.TryGetValue(relativePath, out var beforeContent);
            var hasAfter = afterFiles.TryGetValue(relativePath, out var afterContent);
            if (hasBefore && hasAfter)
            {
                if (!string.Equals(beforeContent, afterContent, StringComparison.Ordinal))
                {
                    fileDiffs.Add(new SkillFileDiff(relativePath, SkillDiffChangeKind.Modified, beforeContent, afterContent));
                }

                continue;
            }

            if (hasAfter)
            {
                fileDiffs.Add(new SkillFileDiff(relativePath, SkillDiffChangeKind.Added, null, afterContent));
                continue;
            }

            fileDiffs.Add(new SkillFileDiff(relativePath, SkillDiffChangeKind.Deleted, beforeContent, null));
        }

        return fileDiffs.Count == 0 ? Array.Empty<SkillActionDiff>() : [new SkillActionDiff(fileDiffs)];
    }

    private static SkillActionFileChanges BuildReplacementFileChanges (
        IReadOnlyDictionary<PackageRelativePath, string> beforeFiles,
        IReadOnlyDictionary<PackageRelativePath, string> afterFiles)
    {
        var replacedFiles = new List<PackageRelativePath>();
        var removedFiles = new List<PackageRelativePath>();

        foreach (var relativePath in beforeFiles.Keys.OrderBy(static path => path.Value, StringComparer.Ordinal))
        {
            var hasAfter = afterFiles.TryGetValue(relativePath, out var afterContent);
            if (!hasAfter)
            {
                removedFiles.Add(relativePath);
                continue;
            }

            if (!string.Equals(beforeFiles[relativePath], afterContent, StringComparison.Ordinal))
            {
                replacedFiles.Add(relativePath);
            }
        }

        return new SkillActionFileChanges(
            replacedFiles.ToArray(),
            removedFiles.ToArray());
    }

    private static Dictionary<PackageRelativePath, string> CreateNormalizedPackageFileMap (SkillMaterializedPackage materializedPackage)
    {
        return materializedPackage.Files.ToDictionary(
            static file => file.RelativePath,
            static file => SkillTextNormalizer.NormalizeToLf(file.Content));
    }

    private static SkillActionTargetSnapshot CreateTargetSnapshot (SkillExistingTargetEntries entries)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var directoryPath in entries.Directories.OrderBy(static path => path.Value, StringComparer.Ordinal))
        {
            AppendSnapshotEntry(hash, "D", directoryPath.Value, content: null);
        }

        foreach (var file in entries.Files.OrderBy(static file => file.Key.Value, StringComparer.Ordinal))
        {
            AppendSnapshotEntry(hash, "F", file.Key.Value, file.Value);
        }

        return new SkillActionTargetSnapshot(Sha256Digest.GetHashAndReset(hash));
    }

    private static void AppendSnapshotEntry (
        IncrementalHash hash,
        string kind,
        string relativePath,
        string? content)
    {
        AppendLengthPrefixedUtf8(hash, kind);
        AppendLengthPrefixedUtf8(hash, relativePath);
        if (content is not null)
        {
            AppendLengthPrefixedUtf8(hash, content);
        }
    }

    private static void AppendLengthPrefixedUtf8 (
        IncrementalHash hash,
        string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> lengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthBytes, bytes.Length);
        hash.AppendData(lengthBytes);
        hash.AppendData(bytes);
    }

    private static async ValueTask<SkillOperationResult<SkillExistingTargetEntries>> ReadExistingTargetEntriesAsync (
        AbsolutePath skillDirectory,
        CancellationToken cancellationToken)
    {
        var files = new Dictionary<PackageRelativePath, string>();
        if (!Directory.Exists(skillDirectory.Value))
        {
            return SkillOperationResult<SkillExistingTargetEntries>.Success(new SkillExistingTargetEntries(
                files,
                Array.Empty<PackageRelativePath>()));
        }

        var skillDirectoryResult = PackagePathResolver.ResolveUnderRoot(skillDirectory, skillDirectory);
        if (!skillDirectoryResult.IsSuccess)
        {
            return SkillOperationResult<SkillExistingTargetEntries>.FailureResult(
                skillDirectoryResult.Failure!.Code,
                skillDirectoryResult.Failure.Message);
        }

        var resolvedSkillDirectory = skillDirectoryResult.Value!;

        try
        {
            var relativeFilePaths = new List<PackageRelativePath>();
            var relativeDirectoryPaths = new List<PackageRelativePath>();
            var entriesResult = ReadExistingEntriesRecursive(
                resolvedSkillDirectory,
                resolvedSkillDirectory,
                relativeFilePaths,
                relativeDirectoryPaths,
                cancellationToken);
            if (!entriesResult.IsSuccess)
            {
                return SkillOperationResult<SkillExistingTargetEntries>.FailureResult(
                    entriesResult.Failure!.Code,
                    entriesResult.Failure.Message);
            }

            var directories = relativeDirectoryPaths.OrderBy(static path => path.Value, StringComparer.Ordinal).ToArray();

            foreach (var relativePath in relativeFilePaths.OrderBy(static path => path.Value, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var resolvedPathResult = PackagePathResolver.ResolveRegularFile(resolvedSkillDirectory, relativePath);
                if (!resolvedPathResult.IsSuccess)
                {
                    return SkillOperationResult<SkillExistingTargetEntries>.FailureResult(
                        resolvedPathResult.Failure!.Code,
                        resolvedPathResult.Failure.Message);
                }

                files[relativePath] = SkillTextNormalizer.NormalizeToLf(
                    await File.ReadAllTextAsync(resolvedPathResult.Value!.Value, cancellationToken).ConfigureAwait(false));
            }

            return SkillOperationResult<SkillExistingTargetEntries>.Success(new SkillExistingTargetEntries(
                files,
                directories));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SkillOperationResult<SkillExistingTargetEntries>.FailureResult(
                SkillFailureCodes.InstallTargetReadFailed,
                $"Failed to read SKILL package diff input: {resolvedSkillDirectory}. {ex.Message}");
        }
    }

    private static SkillOperationResult<bool> ReadExistingEntriesRecursive (
        AbsolutePath skillDirectory,
        AbsolutePath directoryPath,
        List<PackageRelativePath> files,
        List<PackageRelativePath> directories,
        CancellationToken cancellationToken)
    {
        foreach (var entryPathValue in Directory.EnumerateFileSystemEntries(directoryPath.Value).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entryPath = AbsolutePath.Parse(entryPathValue);
            var relativePathValue = Path.GetRelativePath(skillDirectory.Value, entryPath.Value).Replace(Path.DirectorySeparatorChar, '/');
            if (!PackageRelativePath.TryParse(relativePathValue, out var relativePath))
            {
                return SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.PathUnsafe,
                    $"Package path is unsafe: {relativePathValue}");
            }

            if (!FileSystemEntryInspector.TryInspect(
                    entryPath,
                    out var entryObservation,
                    out _))
            {
                return SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.PathUnsafe,
                    $"Package path must be a regular file or directory: {relativePath}");
            }

            if (entryObservation.State == FileSystemEntryState.Directory)
            {
                var resolvedPathResult = PackagePathResolver.ResolveUnderRoot(skillDirectory, entryPath);
                if (!resolvedPathResult.IsSuccess)
                {
                    return SkillOperationResult<bool>.FailureResult(
                        resolvedPathResult.Failure!.Code,
                        $"Package path escaped skill directory: {relativePath}");
                }

                directories.Add(relativePath);
                var directoryResult = ReadExistingEntriesRecursive(
                    skillDirectory,
                    resolvedPathResult.Value!,
                    files,
                    directories,
                    cancellationToken);
                if (!directoryResult.IsSuccess)
                {
                    return directoryResult;
                }

                continue;
            }

            if (entryObservation.State == FileSystemEntryState.RegularFile)
            {
                var resolvedPathResult = PackagePathResolver.ResolveUnderRoot(skillDirectory, entryPath);
                if (!resolvedPathResult.IsSuccess)
                {
                    return SkillOperationResult<bool>.FailureResult(
                        resolvedPathResult.Failure!.Code,
                        $"Package path escaped skill directory: {relativePath}");
                }

                files.Add(relativePath);
                continue;
            }

            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Package path must be a regular file or directory: {relativePath}");
        }

        return SkillOperationResult<bool>.Success(true);
    }

    internal sealed class SkillMaterializedPackageChangePlan
    {
        public SkillMaterializedPackageChangePlan (
            IReadOnlyList<SkillActionDiff> diffs,
            SkillActionFileChangePlan fileChanges)
        {
            Diffs = SkillActionContractGuard.Snapshot(diffs, nameof(diffs));
            FileChanges = fileChanges ?? throw new ArgumentNullException(nameof(fileChanges));
        }

        public IReadOnlyList<SkillActionDiff> Diffs { get; }

        public SkillActionFileChangePlan FileChanges { get; }
    }

    private sealed class SkillExistingTargetEntries
    {
        public SkillExistingTargetEntries (
            IReadOnlyDictionary<PackageRelativePath, string> files,
            IReadOnlyList<PackageRelativePath> directories)
        {
            ArgumentNullException.ThrowIfNull(files);
            if (files.Any(static entry => entry.Value is null))
            {
                throw new ArgumentException("Existing target file content must not be null.", nameof(files));
            }

            ArgumentNullException.ThrowIfNull(directories);
            if (directories.Any(static directory => directory is null))
            {
                throw new ArgumentException("Existing target directories must not contain null items.", nameof(directories));
            }

            Files = new ReadOnlyDictionary<PackageRelativePath, string>(files.ToDictionary(
                static entry => entry.Key,
                static entry => entry.Value));
            Directories = Array.AsReadOnly(directories.ToArray());
        }

        public IReadOnlyDictionary<PackageRelativePath, string> Files { get; }

        public IReadOnlyList<PackageRelativePath> Directories { get; }
    }
}

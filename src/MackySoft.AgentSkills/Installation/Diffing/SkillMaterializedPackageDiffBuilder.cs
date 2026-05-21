using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Materialization;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Diffing;

/// <summary> Builds structured file diffs between an installed target and a materialized package. </summary>
public sealed class SkillMaterializedPackageDiffBuilder
{
    private const string SnapshotDigestPrefix = "sha256:";

    /// <summary> Builds one structured diff for a target directory and desired materialized package. </summary>
    /// <param name="skillDirectory"> The target skill directory. </param>
    /// <param name="materializedPackage"> The desired materialized package. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Structured diffs or a path-safety failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<SkillActionDiff>>> BuildAsync (
        string skillDirectory,
        SkillMaterializedPackage materializedPackage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
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
        string skillDirectory,
        SkillMaterializedPackage materializedPackage,
        bool printDiff,
        CancellationToken cancellationToken = default)
    {
        return printDiff
            ? BuildAsync(skillDirectory, materializedPackage, cancellationToken)
            : ValueTask.FromResult(SkillOperationResult<IReadOnlyList<SkillActionDiff>>.Success(Array.Empty<SkillActionDiff>()));
    }

    /// <summary> Builds replacement file changes and optional structured diffs for one target directory. </summary>
    /// <param name="skillDirectory"> The target skill directory. </param>
    /// <param name="materializedPackage"> The desired materialized package. </param>
    /// <param name="printDiff"> Whether structured diffs should be included. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Replacement file changes and optional diffs, or a path-safety/read failure. </returns>
    internal async ValueTask<SkillOperationResult<SkillMaterializedPackageChangePlan>> BuildReplacementPlanAsync (
        string skillDirectory,
        SkillMaterializedPackage materializedPackage,
        bool printDiff,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
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

    /// <summary> Builds deterministic file changes for deleting one target directory. </summary>
    /// <param name="skillDirectory"> The target skill directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Deletion file changes or a path-safety/read failure. </returns>
    internal async ValueTask<SkillOperationResult<SkillActionFileChangePlan>> BuildDeletionFileChangesAsync (
        string skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
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
                Array.Empty<string>(),
                beforeFiles.Keys.Order(StringComparer.Ordinal).ToArray()),
            CreateTargetSnapshot(beforeEntries)));
    }

    /// <summary> Builds the current target snapshot used by execution preconditions. </summary>
    /// <param name="skillDirectory"> The target skill directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The current target snapshot or a path-safety/read failure. </returns>
    internal async ValueTask<SkillOperationResult<SkillActionTargetSnapshot>> BuildTargetSnapshotAsync (
        string skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
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
        IReadOnlyDictionary<string, string> beforeFiles,
        IReadOnlyDictionary<string, string> afterFiles)
    {
        var relativePaths = beforeFiles.Keys
            .Concat(afterFiles.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

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
        IReadOnlyDictionary<string, string> beforeFiles,
        IReadOnlyDictionary<string, string> afterFiles)
    {
        var replacedFiles = new List<string>();
        var removedFiles = new List<string>();

        foreach (var relativePath in beforeFiles.Keys.Order(StringComparer.Ordinal))
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

    private static Dictionary<string, string> CreateNormalizedPackageFileMap (SkillMaterializedPackage materializedPackage)
    {
        return materializedPackage.Files.ToDictionary(
            static file => file.RelativePath,
            static file => SkillTextNormalizer.NormalizeToLf(file.Content),
            StringComparer.Ordinal);
    }

    private static SkillActionTargetSnapshot CreateTargetSnapshot (SkillExistingTargetEntries entries)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var directoryPath in entries.Directories.Order(StringComparer.Ordinal))
        {
            AppendSnapshotEntry(hash, "D", directoryPath, content: null);
        }

        foreach (var file in entries.Files.OrderBy(static file => file.Key, StringComparer.Ordinal))
        {
            AppendSnapshotEntry(hash, "F", file.Key, file.Value);
        }

        return new SkillActionTargetSnapshot(SnapshotDigestPrefix + ToLowerHex(hash.GetHashAndReset()));
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

    private static string ToLowerHex (byte[] bytes)
    {
        const string HexChars = "0123456789abcdef";

        var chars = new char[bytes.Length * 2];
        var index = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            var value = bytes[i];
            chars[index] = HexChars[value >> 4];
            chars[index + 1] = HexChars[value & 0x0F];
            index += 2;
        }

        return new string(chars);
    }

    private static async ValueTask<SkillOperationResult<SkillExistingTargetEntries>> ReadExistingTargetEntriesAsync (
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var fullSkillDirectory = Path.GetFullPath(skillDirectory);
        if (!Directory.Exists(fullSkillDirectory))
        {
            return SkillOperationResult<SkillExistingTargetEntries>.Success(new SkillExistingTargetEntries(
                files,
                Array.Empty<string>()));
        }

        var skillDirectoryResult = SkillPackagePathBoundary.ResolveUnderRoot(fullSkillDirectory, fullSkillDirectory);
        if (!skillDirectoryResult.IsSuccess)
        {
            return SkillOperationResult<SkillExistingTargetEntries>.FailureResult(
                skillDirectoryResult.Failure!.Code,
                skillDirectoryResult.Failure.Message);
        }

        var resolvedSkillDirectory = skillDirectoryResult.Value!;

        try
        {
            var relativeFilePaths = new List<string>();
            var relativeDirectoryPaths = new List<string>();
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

            var directories = relativeDirectoryPaths.Order(StringComparer.Ordinal).ToArray();
            foreach (var directoryPath in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var directoryPathResult = ValidateSafeRelativePath(directoryPath);
                if (!directoryPathResult.IsSuccess)
                {
                    return SkillOperationResult<SkillExistingTargetEntries>.FailureResult(
                        directoryPathResult.Failure!.Code,
                        directoryPathResult.Failure.Message);
                }
            }

            foreach (var relativePath in relativeFilePaths.Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var resolvedPathResult = SkillPackageRegularFileResolver.ResolvePackageFilePath(resolvedSkillDirectory, relativePath);
                if (!resolvedPathResult.IsSuccess)
                {
                    return SkillOperationResult<SkillExistingTargetEntries>.FailureResult(
                        resolvedPathResult.Failure!.Code,
                        resolvedPathResult.Failure.Message);
                }

                files[relativePath] = SkillTextNormalizer.NormalizeToLf(
                    await File.ReadAllTextAsync(resolvedPathResult.Value!, cancellationToken).ConfigureAwait(false));
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
        string skillDirectory,
        string directoryPath,
        List<string> files,
        List<string> directories,
        CancellationToken cancellationToken)
    {
        foreach (var entryPath in Directory.EnumerateFileSystemEntries(directoryPath).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(skillDirectory, entryPath).Replace(Path.DirectorySeparatorChar, '/');
            if (SkillPackageFileSystemEntryGuard.IsDirectory(entryPath))
            {
                var resolvedPathResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, entryPath);
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

            if (SkillPackageFileSystemEntryGuard.IsRegularFile(entryPath))
            {
                var resolvedPathResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, entryPath);
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

    private static SkillOperationResult<bool> ValidateSafeRelativePath (string relativePath)
    {
        return SkillRelativePath.IsSafeFilePath(relativePath)
            ? SkillOperationResult<bool>.Success(true)
            : SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Package path is unsafe: {relativePath}");
    }

    internal sealed record SkillMaterializedPackageChangePlan (
        IReadOnlyList<SkillActionDiff> Diffs,
        SkillActionFileChangePlan FileChanges);

    private sealed record SkillExistingTargetEntries (
        IReadOnlyDictionary<string, string> Files,
        IReadOnlyList<string> Directories);
}

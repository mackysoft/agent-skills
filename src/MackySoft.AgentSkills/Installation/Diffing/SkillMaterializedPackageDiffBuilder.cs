using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Materialization;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

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
        string skillDirectory,
        SkillMaterializedPackage materializedPackage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(materializedPackage);
        cancellationToken.ThrowIfCancellationRequested();

        var beforeResult = await ReadExistingFilesAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!beforeResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<SkillActionDiff>>.FailureResult(
                beforeResult.Failure!.Code,
                beforeResult.Failure.Message);
        }

        var beforeFiles = beforeResult.Value!;
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

        var beforeResult = await ReadExistingFilesAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!beforeResult.IsSuccess)
        {
            return SkillOperationResult<SkillMaterializedPackageChangePlan>.FailureResult(
                beforeResult.Failure!.Code,
                beforeResult.Failure.Message);
        }

        var beforeFiles = beforeResult.Value!;
        var afterFiles = CreateNormalizedPackageFileMap(materializedPackage);
        var diffs = printDiff ? BuildDiffs(beforeFiles, afterFiles) : Array.Empty<SkillActionDiff>();
        var fileChanges = BuildReplacementFileChanges(beforeFiles, afterFiles);

        return SkillOperationResult<SkillMaterializedPackageChangePlan>.Success(new SkillMaterializedPackageChangePlan(
            diffs,
            fileChanges));
    }

    /// <summary> Builds deterministic file changes for deleting one target directory. </summary>
    /// <param name="skillDirectory"> The target skill directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Deletion file changes or a path-safety/read failure. </returns>
    internal async ValueTask<SkillOperationResult<SkillActionFileChanges>> BuildDeletionFileChangesAsync (
        string skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var beforeResult = await ReadExistingFilesAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!beforeResult.IsSuccess)
        {
            return SkillOperationResult<SkillActionFileChanges>.FailureResult(
                beforeResult.Failure!.Code,
                beforeResult.Failure.Message);
        }

        return SkillOperationResult<SkillActionFileChanges>.Success(new SkillActionFileChanges(
            Array.Empty<string>(),
            beforeResult.Value!.Keys.Order(StringComparer.Ordinal).ToArray()));
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

    private static async ValueTask<SkillOperationResult<Dictionary<string, string>>> ReadExistingFilesAsync (
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var fullSkillDirectory = Path.GetFullPath(skillDirectory);
        if (!Directory.Exists(fullSkillDirectory))
        {
            return SkillOperationResult<Dictionary<string, string>>.Success(files);
        }

        var skillDirectoryResult = SkillPackagePathBoundary.ResolveUnderRoot(fullSkillDirectory, fullSkillDirectory);
        if (!skillDirectoryResult.IsSuccess)
        {
            return SkillOperationResult<Dictionary<string, string>>.FailureResult(
                skillDirectoryResult.Failure!.Code,
                skillDirectoryResult.Failure.Message);
        }

        var resolvedSkillDirectory = skillDirectoryResult.Value!;

        try
        {
            foreach (var filePath in Directory.EnumerateFiles(resolvedSkillDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(resolvedSkillDirectory, filePath).Replace(Path.DirectorySeparatorChar, '/');
                var regularFileResult = SkillPackageRegularFileResolver.VerifyRegularFile(filePath, relativePath);
                if (!regularFileResult.IsSuccess)
                {
                    return SkillOperationResult<Dictionary<string, string>>.FailureResult(
                        regularFileResult.Failure!.Code,
                        regularFileResult.Failure.Message);
                }

                var resolvedPathResult = SkillPackagePathBoundary.ResolveUnderRoot(resolvedSkillDirectory, filePath);
                if (!resolvedPathResult.IsSuccess)
                {
                    return SkillOperationResult<Dictionary<string, string>>.FailureResult(
                        resolvedPathResult.Failure!.Code,
                        resolvedPathResult.Failure.Message);
                }

                files[relativePath] = SkillTextNormalizer.NormalizeToLf(
                    await File.ReadAllTextAsync(resolvedPathResult.Value!, cancellationToken).ConfigureAwait(false));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SkillOperationResult<Dictionary<string, string>>.FailureResult(
                SkillFailureCodes.InstallTargetReadFailed,
                $"Failed to read SKILL package diff input: {resolvedSkillDirectory}. {ex.Message}");
        }

        return SkillOperationResult<Dictionary<string, string>>.Success(files);
    }

    internal sealed record SkillMaterializedPackageChangePlan (
        IReadOnlyList<SkillActionDiff> Diffs,
        SkillActionFileChanges FileChanges);
}

using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Validation;

/// <summary> Verifies that an installed SKILL directory contains exactly the expected materialized files. </summary>
public sealed class SkillInstalledFileSetVerifier
{
    /// <summary> Verifies the installed file set against one materialized package. </summary>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="expectedFiles"> The host-materialized file set expected for this directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The structured file-set verification result, or a hard path-safety failure. </returns>
    public ValueTask<SkillOperationResult<SkillInstalledFileSetVerificationResult>> VerifyAsync (
        string skillDirectory,
        IReadOnlyCollection<SkillPackageFile> expectedFiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(expectedFiles);
        cancellationToken.ThrowIfCancellationRequested();

        var expectedRelativePaths = expectedFiles
            .Select(static file => file.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var entriesResult = ReadInstalledEntries(skillDirectory, cancellationToken);
        if (!entriesResult.IsSuccess)
        {
            return FailureValueTask(entriesResult.Failure!.Code, entriesResult.Failure.Message);
        }

        var result = VerifyInstalledEntries(
            skillDirectory,
            expectedRelativePaths,
            Array.Empty<string>(),
            entriesResult.Value!,
            cancellationToken);

        return ValueTask.FromResult(result);
    }

    internal static SkillOperationResult<SkillInstalledFileSetVerificationResult> VerifyInstalledEntries (
        string skillDirectory,
        IReadOnlyCollection<string> requiredRelativePaths,
        IReadOnlyCollection<string> managedFilePrefixes,
        SkillInstalledFileSetEntries installedEntries,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(requiredRelativePaths);
        ArgumentNullException.ThrowIfNull(managedFilePrefixes);
        ArgumentNullException.ThrowIfNull(installedEntries);
        cancellationToken.ThrowIfCancellationRequested();

        var requiredPathSet = requiredRelativePaths.ToHashSet(StringComparer.Ordinal);
        var managedPrefixes = managedFilePrefixes.ToArray();
        var explainedDirectoryPaths = SkillInstalledDirectorySet.BuildParentDirectories(requiredPathSet);
        var missingFiles = new List<string>();
        var extraFiles = new List<string>();

        foreach (var requiredPath in requiredPathSet.Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var requiredPathResult = SkillPackageRegularFileResolver.ResolvePackageFilePath(skillDirectory, requiredPath);
            if (!requiredPathResult.IsSuccess)
            {
                return SkillOperationResult<SkillInstalledFileSetVerificationResult>.FailureResult(
                    requiredPathResult.Failure!.Code,
                    requiredPathResult.Failure.Message);
            }

            if (!File.Exists(requiredPathResult.Value!))
            {
                missingFiles.Add(requiredPath);
            }
        }

        foreach (var relativePath in installedEntries.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SkillInstalledDirectorySet.AddParentDirectories(explainedDirectoryPaths, relativePath);

            if (!requiredPathSet.Contains(relativePath) && !StartsWithAny(relativePath, managedPrefixes))
            {
                extraFiles.Add(relativePath);
            }
        }

        var extraDirectories = GetExtraDirectories(installedEntries.Directories, explainedDirectoryPaths);

        return SkillOperationResult<SkillInstalledFileSetVerificationResult>.Success(new SkillInstalledFileSetVerificationResult(
            missingFiles.Order(StringComparer.Ordinal).ToArray(),
            extraFiles.Order(StringComparer.Ordinal).ToArray(),
            extraDirectories));
    }

    internal static SkillOperationResult<SkillInstalledFileSetEntries> ReadInstalledEntries (
        string skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var skillDirectoryResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, skillDirectory);
        if (!skillDirectoryResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledFileSetEntries>.FailureResult(
                skillDirectoryResult.Failure!.Code,
                skillDirectoryResult.Failure.Message);
        }

        var resolvedSkillDirectory = skillDirectoryResult.Value!;
        var files = new List<string>();
        var directories = new List<string>();
        var result = ReadInstalledEntriesRecursive(resolvedSkillDirectory, resolvedSkillDirectory, files, directories, cancellationToken);
        return result.IsSuccess
            ? SkillOperationResult<SkillInstalledFileSetEntries>.Success(new SkillInstalledFileSetEntries(
                files.Order(StringComparer.Ordinal).ToArray(),
                directories.Order(StringComparer.Ordinal).ToArray()))
            : SkillOperationResult<SkillInstalledFileSetEntries>.FailureResult(result.Failure!.Code, result.Failure.Message);
    }

    internal static IReadOnlyList<string> GetExtraDirectories (
        IReadOnlyCollection<string> installedDirectoryPaths,
        IReadOnlySet<string> explainedDirectoryPaths)
    {
        ArgumentNullException.ThrowIfNull(installedDirectoryPaths);
        ArgumentNullException.ThrowIfNull(explainedDirectoryPaths);

        return installedDirectoryPaths
            .Where(directoryPath => !explainedDirectoryPaths.Contains(directoryPath))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static SkillOperationResult<bool> ReadInstalledEntriesRecursive (
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
                var directoryResult = ReadInstalledEntriesRecursive(
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

    private static bool StartsWithAny (
        string relativePath,
        IReadOnlyCollection<string> prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (relativePath.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ValueTask<SkillOperationResult<SkillInstalledFileSetVerificationResult>> FailureValueTask (
        SkillFailureCode code,
        string message)
    {
        return ValueTask.FromResult(SkillOperationResult<SkillInstalledFileSetVerificationResult>.FailureResult(code, message));
    }

    internal sealed class SkillInstalledFileSetEntries
    {
        internal SkillInstalledFileSetEntries (
            IReadOnlyList<string> files,
            IReadOnlyList<string> directories)
        {
            Files = SkillInstalledFileSetPathSnapshot.Create(files, nameof(files));
            Directories = SkillInstalledFileSetPathSnapshot.Create(directories, nameof(directories));
        }

        public IReadOnlyList<string> Files { get; }

        public IReadOnlyList<string> Directories { get; }
    }
}

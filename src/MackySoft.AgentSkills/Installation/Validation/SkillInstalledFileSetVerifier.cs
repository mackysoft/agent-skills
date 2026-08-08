using MackySoft.AgentSkills.Packaging.Paths;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

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
        AbsolutePath skillDirectory,
        IReadOnlyCollection<PackageTextFile> expectedFiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillDirectory);
        ArgumentNullException.ThrowIfNull(expectedFiles);
        cancellationToken.ThrowIfCancellationRequested();

        var expectedRelativePaths = expectedFiles
            .Select(static file => file.RelativePath)
            .ToHashSet();
        var entriesResult = ReadInstalledEntries(skillDirectory, cancellationToken);
        if (!entriesResult.IsSuccess)
        {
            return FailureValueTask(entriesResult.Failure!.Code, entriesResult.Failure.Message);
        }

        var result = VerifyInstalledEntries(
            skillDirectory,
            expectedRelativePaths,
            Array.Empty<PackageRelativePath>(),
            entriesResult.Value!,
            cancellationToken);

        return ValueTask.FromResult(result);
    }

    internal static SkillOperationResult<SkillInstalledFileSetVerificationResult> VerifyInstalledEntries (
        AbsolutePath skillDirectory,
        IReadOnlyCollection<PackageRelativePath> requiredRelativePaths,
        IReadOnlyCollection<PackageRelativePath> managedDirectoryPaths,
        SkillInstalledFileSetEntries installedEntries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillDirectory);
        ArgumentNullException.ThrowIfNull(requiredRelativePaths);
        ArgumentNullException.ThrowIfNull(managedDirectoryPaths);
        ArgumentNullException.ThrowIfNull(installedEntries);
        cancellationToken.ThrowIfCancellationRequested();

        var requiredPathSet = requiredRelativePaths.ToHashSet();
        var managedDirectories = managedDirectoryPaths.ToArray();
        var explainedDirectoryPaths = SkillInstalledDirectorySet.BuildParentDirectories(requiredPathSet);
        var missingFiles = new List<PackageRelativePath>();
        var extraFiles = new List<PackageRelativePath>();

        foreach (var requiredPath in requiredPathSet.OrderBy(static path => path.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var requiredPathResult = PackagePathResolver.ResolveRegularFile(skillDirectory, requiredPath);
            if (!requiredPathResult.IsSuccess)
            {
                return SkillOperationResult<SkillInstalledFileSetVerificationResult>.FailureResult(
                    requiredPathResult.Failure!.Code,
                    requiredPathResult.Failure.Message);
            }

            if (!File.Exists(requiredPathResult.Value!.Value))
            {
                missingFiles.Add(requiredPath);
            }
        }

        foreach (var relativePath in installedEntries.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SkillInstalledDirectorySet.AddParentDirectories(explainedDirectoryPaths, relativePath);

            if (!requiredPathSet.Contains(relativePath) && !IsBelowAny(relativePath, managedDirectories))
            {
                extraFiles.Add(relativePath);
            }
        }

        var extraDirectories = GetExtraDirectories(installedEntries.Directories, explainedDirectoryPaths);

        return SkillOperationResult<SkillInstalledFileSetVerificationResult>.Success(new SkillInstalledFileSetVerificationResult(
            missingFiles.OrderBy(static path => path.Value, StringComparer.Ordinal).ToArray(),
            extraFiles.OrderBy(static path => path.Value, StringComparer.Ordinal).ToArray(),
            extraDirectories));
    }

    internal static SkillOperationResult<SkillInstalledFileSetEntries> ReadInstalledEntries (
        AbsolutePath skillDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var skillDirectoryResult = PackagePathResolver.ResolveUnderRoot(skillDirectory, skillDirectory);
        if (!skillDirectoryResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledFileSetEntries>.FailureResult(
                skillDirectoryResult.Failure!.Code,
                skillDirectoryResult.Failure.Message);
        }

        var resolvedSkillDirectory = skillDirectoryResult.Value!;
        var files = new List<PackageRelativePath>();
        var directories = new List<PackageRelativePath>();
        var result = ReadInstalledEntriesRecursive(resolvedSkillDirectory, resolvedSkillDirectory, files, directories, cancellationToken);
        return result.IsSuccess
            ? SkillOperationResult<SkillInstalledFileSetEntries>.Success(new SkillInstalledFileSetEntries(
                files.OrderBy(static path => path.Value, StringComparer.Ordinal).ToArray(),
                directories.OrderBy(static path => path.Value, StringComparer.Ordinal).ToArray()))
            : SkillOperationResult<SkillInstalledFileSetEntries>.FailureResult(result.Failure!.Code, result.Failure.Message);
    }

    internal static IReadOnlyList<PackageRelativePath> GetExtraDirectories (
        IReadOnlyCollection<PackageRelativePath> installedDirectoryPaths,
        IReadOnlySet<PackageRelativePath> explainedDirectoryPaths)
    {
        ArgumentNullException.ThrowIfNull(installedDirectoryPaths);
        ArgumentNullException.ThrowIfNull(explainedDirectoryPaths);

        return installedDirectoryPaths
            .Where(directoryPath => !explainedDirectoryPaths.Contains(directoryPath))
            .OrderBy(static path => path.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static SkillOperationResult<bool> ReadInstalledEntriesRecursive (
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
                    $"Package path must be a canonical package-relative path: {relativePathValue}");
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

    private static bool IsBelowAny (
        PackageRelativePath relativePath,
        IReadOnlyCollection<PackageRelativePath> directoryPaths)
    {
        foreach (var directoryPath in directoryPaths)
        {
            if (relativePath.IsDescendantOf(directoryPath))
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
            IReadOnlyList<PackageRelativePath> files,
            IReadOnlyList<PackageRelativePath> directories)
        {
            Files = SkillInstalledFileSetPathSnapshot.Create(files, nameof(files));
            Directories = SkillInstalledFileSetPathSnapshot.Create(directories, nameof(directories));
        }

        public IReadOnlyList<PackageRelativePath> Files { get; }

        public IReadOnlyList<PackageRelativePath> Directories { get; }
    }
}

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
    public async ValueTask<SkillOperationResult<SkillInstalledFileSetVerificationResult>> VerifyAsync (
        string skillDirectory,
        IReadOnlyCollection<SkillPackageFile> expectedFiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(expectedFiles);
        cancellationToken.ThrowIfCancellationRequested();

        var expectedFileByPath = expectedFiles.ToDictionary(
            static file => file.RelativePath,
            static file => SkillTextNormalizer.NormalizeToLf(file.Content),
            StringComparer.Ordinal);
        var expectedRelativePaths = expectedFileByPath.Keys.ToHashSet(StringComparer.Ordinal);
        var expectedDirectoryPaths = SkillInstalledDirectorySet.BuildParentDirectories(expectedRelativePaths);
        var missingFiles = new List<string>();
        var extraFiles = new List<string>();
        var mismatchedFiles = new List<string>();

        foreach (var expectedRelativePath in expectedRelativePaths.Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var expectedPathResult = SkillPackageRegularFileResolver.ResolvePackageFilePath(skillDirectory, expectedRelativePath);
            if (!expectedPathResult.IsSuccess)
            {
                return Failure(expectedPathResult.Failure!.Code, expectedPathResult.Failure.Message);
            }

            if (!File.Exists(expectedPathResult.Value!))
            {
                missingFiles.Add(expectedRelativePath);
                continue;
            }

            var installedContent = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(expectedPathResult.Value!, cancellationToken).ConfigureAwait(false));
            if (!string.Equals(installedContent, expectedFileByPath[expectedRelativePath], StringComparison.Ordinal))
            {
                mismatchedFiles.Add(expectedRelativePath);
            }
        }

        foreach (var filePath in Directory.EnumerateFiles(skillDirectory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(skillDirectory, filePath).Replace(Path.DirectorySeparatorChar, '/');
            var regularFileResult = SkillPackageRegularFileResolver.VerifyRegularFile(filePath, relativePath);
            if (!regularFileResult.IsSuccess)
            {
                return Failure(regularFileResult.Failure!.Code, regularFileResult.Failure.Message);
            }

            var filePathResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, filePath);
            if (!filePathResult.IsSuccess)
            {
                return Failure(filePathResult.Failure!.Code, filePathResult.Failure.Message);
            }

            SkillInstalledDirectorySet.AddParentDirectories(expectedDirectoryPaths, relativePath);

            if (!expectedRelativePaths.Contains(relativePath))
            {
                extraFiles.Add(relativePath);
            }
        }

        var extraDirectoriesResult = GetExtraDirectories(skillDirectory, expectedDirectoryPaths, cancellationToken);
        if (!extraDirectoriesResult.IsSuccess)
        {
            return Failure(extraDirectoriesResult.Failure!.Code, extraDirectoriesResult.Failure.Message);
        }

        return SkillOperationResult<SkillInstalledFileSetVerificationResult>.Success(new SkillInstalledFileSetVerificationResult(
            missingFiles.Order(StringComparer.Ordinal).ToArray(),
            extraFiles.Order(StringComparer.Ordinal).ToArray(),
            mismatchedFiles.Order(StringComparer.Ordinal).ToArray(),
            extraDirectoriesResult.Value!));
    }

    internal static SkillOperationResult<IReadOnlyList<string>> GetExtraDirectories (
        string skillDirectory,
        IReadOnlySet<string> explainedDirectoryPaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(explainedDirectoryPaths);

        var extraDirectories = new List<string>();
        foreach (var directoryPath in Directory.EnumerateDirectories(skillDirectory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directoryPathResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, directoryPath);
            if (!directoryPathResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<string>>.FailureResult(directoryPathResult.Failure!.Code, directoryPathResult.Failure.Message);
            }

            var relativePath = Path.GetRelativePath(skillDirectory, Path.GetFullPath(directoryPath)).Replace(Path.DirectorySeparatorChar, '/');
            if (!SkillPackageFileSystemEntryGuard.IsDirectory(directoryPath))
            {
                return SkillOperationResult<IReadOnlyList<string>>.FailureResult(
                    SkillFailureCodes.PathUnsafe,
                    $"Package directory must be a regular directory: {relativePath}");
            }

            if (!explainedDirectoryPaths.Contains(relativePath))
            {
                extraDirectories.Add(relativePath);
            }
        }

        return SkillOperationResult<IReadOnlyList<string>>.Success(extraDirectories
            .Order(StringComparer.Ordinal)
            .ToArray());
    }

    private static SkillOperationResult<SkillInstalledFileSetVerificationResult> Failure (
        SkillFailureCode code,
        string message)
    {
        return SkillOperationResult<SkillInstalledFileSetVerificationResult>.FailureResult(code, message);
    }
}

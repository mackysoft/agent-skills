using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Validation;

/// <summary> Verifies that an installed SKILL directory contains exactly the expected materialized files. </summary>
public sealed class SkillInstalledFileSetVerifier
{
    /// <summary> Checks the installed file set against one materialized package. </summary>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="expectedFiles"> The host-materialized file set expected for this directory. </param>
    /// <returns> <see langword="true" /> when the installed file set is exact; otherwise <see langword="false" />. </returns>
    public SkillOperationResult<bool> MatchesExpectedFiles (
        string skillDirectory,
        IReadOnlyCollection<SkillPackageFile> expectedFiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentNullException.ThrowIfNull(expectedFiles);

        var expectedRelativePaths = expectedFiles
            .Select(static file => file.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var expectedDirectoryPaths = SkillInstalledDirectorySet.BuildParentDirectories(expectedRelativePaths);

        foreach (var expectedRelativePath in expectedRelativePaths.Order(StringComparer.Ordinal))
        {
            var expectedPathResult = SkillPackageRegularFileResolver.ResolvePackageFilePath(skillDirectory, expectedRelativePath);
            if (!expectedPathResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(expectedPathResult.Failure!.Code, expectedPathResult.Failure.Message);
            }

            if (!File.Exists(expectedPathResult.Value!))
            {
                return SkillOperationResult<bool>.Success(false);
            }
        }

        foreach (var filePath in Directory.EnumerateFiles(skillDirectory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relativePath = Path.GetRelativePath(skillDirectory, filePath).Replace(Path.DirectorySeparatorChar, '/');
            var regularFileResult = SkillPackageRegularFileResolver.VerifyRegularFile(filePath, relativePath);
            if (!regularFileResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(regularFileResult.Failure!.Code, regularFileResult.Failure.Message);
            }

            var filePathResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, filePath);
            if (!filePathResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(filePathResult.Failure!.Code, filePathResult.Failure.Message);
            }

            if (!expectedRelativePaths.Contains(relativePath))
            {
                return SkillOperationResult<bool>.Success(false);
            }
        }

        var directorySetResult = SkillInstalledDirectorySet.ContainsOnlyAllowedDirectories(skillDirectory, expectedDirectoryPaths);
        if (!directorySetResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(directorySetResult.Failure!.Code, directorySetResult.Failure.Message);
        }

        return directorySetResult;
    }
}

using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.FileSystem;

namespace MackySoft.AgentSkills.Packaging.FileSystem;

/// <summary> Validates filesystem paths against an allowed root boundary. </summary>
internal static class SkillPackagePathBoundary
{
    /// <summary> Resolves a target path and verifies that it remains under the allowed root. </summary>
    /// <param name="rootPath"> The allowed root path. </param>
    /// <param name="targetPath"> The target path. </param>
    /// <returns> The canonical target path or path-safety failure. </returns>
    public static SkillOperationResult<string> ResolveUnderRoot (
        string rootPath,
        string targetPath)
    {
        return SkillPathBoundary.ResolveUnderRoot(
            rootPath,
            targetPath,
            SkillFailureCodes.PathUnsafe,
            "Path");
    }

    /// <summary> Verifies that a package file path remains under the target directory. </summary>
    /// <param name="targetDirectory"> The target directory. </param>
    /// <param name="relativePath"> The package-relative path. </param>
    /// <returns> The canonical file path or path-safety failure. </returns>
    public static SkillOperationResult<string> ResolvePackageFilePath (
        string targetDirectory,
        string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (!SkillRelativePath.IsSafeFilePath(relativePath))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Package file path is unsafe: {relativePath}");
        }

        return ResolveUnderRoot(targetDirectory, Path.Combine(targetDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    /// <summary> Verifies that a package directory name is a safe single path segment under a root. </summary>
    /// <param name="rootDirectory"> The root directory. </param>
    /// <param name="directoryName"> The package directory name. </param>
    /// <returns> The canonical package directory path or path-safety failure. </returns>
    public static SkillOperationResult<string> ResolvePackageDirectory (
        string rootDirectory,
        string directoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryName);

        if (!SkillRelativePath.IsSafePathSegment(directoryName))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Package directory name is unsafe: {directoryName}");
        }

        return ResolveUnderRoot(rootDirectory, Path.Combine(rootDirectory, directoryName));
    }

    /// <summary> Verifies that a package file path remains under both the root and target directory. </summary>
    /// <param name="rootDirectory"> The root directory. </param>
    /// <param name="targetDirectory"> The target package directory. </param>
    /// <param name="relativePath"> The package-relative path. </param>
    /// <returns> The canonical file path or path-safety failure. </returns>
    public static SkillOperationResult<string> ResolvePackageFilePathUnderRoot (
        string rootDirectory,
        string targetDirectory,
        string relativePath)
    {
        var targetResult = ResolveUnderRoot(rootDirectory, targetDirectory);
        if (!targetResult.IsSuccess)
        {
            return targetResult;
        }

        var fileResult = ResolvePackageFilePath(targetResult.Value!, relativePath);
        if (!fileResult.IsSuccess)
        {
            return fileResult;
        }

        return ResolveUnderRoot(rootDirectory, fileResult.Value!);
    }
}

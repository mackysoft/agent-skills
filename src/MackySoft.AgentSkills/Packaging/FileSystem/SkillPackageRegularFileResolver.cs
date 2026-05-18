using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Packaging.FileSystem;

internal static class SkillPackageRegularFileResolver
{
    public static SkillOperationResult<string> ResolvePackageFilePath (
        string packageDirectory,
        string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (!SkillRelativePath.IsSafeFilePath(relativePath))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Package file path is unsafe: {relativePath}");
        }

        var rawPath = Path.GetFullPath(Path.Combine(packageDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if ((File.Exists(rawPath) || Directory.Exists(rawPath))
            && !SkillPackageFileSystemEntryGuard.IsRegularFile(rawPath))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Package file must be a regular file: {relativePath}");
        }

        return SkillPackagePathBoundary.ResolveUnderRoot(packageDirectory, rawPath);
    }

    public static SkillOperationResult<bool> VerifyRegularFile (
        string filePath,
        string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return SkillPackageFileSystemEntryGuard.IsRegularFile(filePath)
            ? SkillOperationResult<bool>.Success(true)
            : SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Package file must be a regular file: {relativePath}");
    }
}

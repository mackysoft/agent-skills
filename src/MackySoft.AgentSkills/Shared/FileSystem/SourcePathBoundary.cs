using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Shared.FileSystem;

/// <summary> Resolves authored source paths and validates their physical node kinds. </summary>
internal static class SourcePathBoundary
{
    /// <summary> Parses a source root without requiring it to exist. </summary>
    internal static SkillOperationResult<AbsolutePath> ParseRoot (
        string rootPath,
        string pathDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathDescription);

        try
        {
            var fullPath = Path.GetFullPath(rootPath);
            return AbsolutePath.TryParse(fullPath, out var root, out var failure)
                ? SkillOperationResult<AbsolutePath>.Success(root)
                : Failure<AbsolutePath>(pathDescription, failure.Message);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException or PathTooLongException)
        {
            return Failure<AbsolutePath>(pathDescription, exception.Message);
        }
    }

    /// <summary> Validates that a parsed source root is a regular directory. </summary>
    internal static SkillOperationResult<AbsolutePath> ValidateDirectoryRoot (
        AbsolutePath root,
        string pathDescription)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathDescription);

        return SkillPackageFileSystemEntryGuard.IsDirectory(root.Value)
            ? SkillOperationResult<AbsolutePath>.Success(root)
            : Failure<AbsolutePath>(pathDescription, "The entry must be a regular directory and must not be a reparse point.");
    }

    /// <summary> Resolves a regular directory at or below an established source root. </summary>
    internal static SkillOperationResult<AbsolutePath> ResolveDirectory (
        AbsolutePath root,
        string relativePath,
        string pathDescription)
    {
        return ResolveEntry(
            root,
            relativePath,
            pathDescription,
            SkillPackageFileSystemEntryGuard.IsDirectory,
            "a regular directory");
    }

    /// <summary> Resolves a regular file at or below an established source root. </summary>
    internal static SkillOperationResult<AbsolutePath> ResolveRegularFile (
        AbsolutePath root,
        string relativePath,
        string pathDescription)
    {
        return ResolveEntry(
            root,
            relativePath,
            pathDescription,
            SkillPackageFileSystemEntryGuard.IsRegularFile,
            "a regular file");
    }

    /// <summary> Returns whether a file-system entry currently exists at the guarded path. </summary>
    internal static bool EntryExists (AbsolutePath path)
    {
        try
        {
            _ = File.GetAttributes(path.Value);
            return true;
        }
        catch (FileNotFoundException)
        {
            return ExistsInParentDirectory(path);
        }
        catch (DirectoryNotFoundException)
        {
            return ExistsInParentDirectory(path);
        }
    }

    private static bool ExistsInParentDirectory (AbsolutePath path)
    {
        if (!path.TryGetParent(out var parent)
            || !Directory.Exists(parent.Value))
        {
            return false;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(parent.Value))
        {
            if (AbsolutePath.TryParse(entry, out var candidate, out _)
                && candidate.IsSameAs(path))
            {
                return true;
            }
        }

        return false;
    }

    private static SkillOperationResult<AbsolutePath> ResolveEntry (
        AbsolutePath root,
        string relativePath,
        string pathDescription,
        Func<string, bool> isSupportedEntry,
        string expectedEntryKind)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathDescription);
        ArgumentNullException.ThrowIfNull(isSupportedEntry);

        if (!RootRelativePath.TryParse(relativePath, out var relative, out var relativeFailure)
            || relative.IsRoot
            || !string.Equals(relative.Value, relativePath, StringComparison.Ordinal))
        {
            var message = relativeFailure.Message ?? "The path is not canonical root-relative text.";
            return Failure<AbsolutePath>(pathDescription, message);
        }

        var contained = ContainedPath.Create(root, relative);
        return isSupportedEntry(contained.Target.Value)
            ? SkillOperationResult<AbsolutePath>.Success(contained.Target)
            : Failure<AbsolutePath>(pathDescription, $"The entry must be {expectedEntryKind} and must not be a reparse point.");
    }

    private static SkillOperationResult<T> Failure<T> (
        string pathDescription,
        string message)
    {
        return SkillOperationResult<T>.FailureResult(
            SkillFailureCodes.SourceInvalid,
            $"{pathDescription} is invalid: {message}");
    }
}

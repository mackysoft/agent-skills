using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Paths;

/// <summary>Applies authored-source containment and physical entry policy to typed paths.</summary>
internal static class AuthoredSourcePathResolver
{
    /// <summary>Validates that a parsed source root is a regular directory.</summary>
    internal static SkillOperationResult<AbsolutePath> ValidateDirectoryRoot (
        AbsolutePath root,
        string pathDescription)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathDescription);

        if (!FileSystemEntryInspector.TryInspect(root, out var observation, out var inspectionFailure))
        {
            return Failure<AbsolutePath>(pathDescription, inspectionFailure.Message);
        }

        return observation.State == FileSystemEntryState.Directory
            ? SkillOperationResult<AbsolutePath>.Success(root)
            : Failure<AbsolutePath>(pathDescription, "The entry must be a regular directory and must not be a reparse point.");
    }

    /// <summary>Resolves a regular directory at or below an established source root.</summary>
    internal static SkillOperationResult<AbsolutePath> ResolveDirectory (
        AbsolutePath root,
        RootRelativePath relativePath,
        string pathDescription)
    {
        return ResolveEntry(
            root,
            relativePath,
            pathDescription,
            FileSystemEntryState.Directory,
            "a regular directory");
    }

    /// <summary>Resolves a regular file at or below an established source root.</summary>
    internal static SkillOperationResult<AbsolutePath> ResolveRegularFile (
        AbsolutePath root,
        RootRelativePath relativePath,
        string pathDescription)
    {
        return ResolveEntry(
            root,
            relativePath,
            pathDescription,
            FileSystemEntryState.RegularFile,
            "a regular file");
    }

    /// <summary>Returns whether a file-system entry currently exists at the guarded path.</summary>
    internal static bool EntryExists (AbsolutePath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return !FileSystemEntryInspector.TryInspect(path, out var observation, out _)
            || observation.State != FileSystemEntryState.Missing;
    }

    private static SkillOperationResult<AbsolutePath> ResolveEntry (
        AbsolutePath root,
        RootRelativePath relativePath,
        string pathDescription,
        FileSystemEntryState expectedEntryState,
        string expectedEntryKind)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathDescription);
        if (relativePath.IsRoot)
        {
            return Failure<AbsolutePath>(pathDescription, "The path must identify an entry below the source root.");
        }

        var contained = ContainedPath.Create(root, relativePath);
        if (!PhysicalPathResolver.TryResolve(
                contained,
                SymbolicLinkHandling.Reject,
                MissingPathHandling.Reject,
                out var resolution,
                out var resolutionFailure))
        {
            return Failure<AbsolutePath>(pathDescription, resolutionFailure.Message);
        }

        return resolution.TargetObservation.State == expectedEntryState
            ? SkillOperationResult<AbsolutePath>.Success(resolution.RequestedPath.Target)
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

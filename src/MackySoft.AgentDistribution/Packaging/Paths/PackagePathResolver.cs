using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Packaging.Paths;

/// <summary>Applies Agent Distribution package containment and entry-kind policy to typed paths.</summary>
internal static class PackagePathResolver
{
    /// <summary>Validates one package path against its lexical and physical root boundary.</summary>
    internal static SkillOperationResult<AbsolutePath> ResolveUnderRoot (
        AbsolutePath rootPath,
        AbsolutePath targetPath)
    {
        ArgumentNullException.ThrowIfNull(rootPath);
        ArgumentNullException.ThrowIfNull(targetPath);

        try
        {
            if (!ContainedPath.TryCreate(rootPath, targetPath, out var containedPath, out var targetFailure))
            {
                return Failure($"Path is invalid: {targetFailure.Message}");
            }

            if (!PhysicalPathResolver.TryResolve(
                    containedPath,
                    SymbolicLinkHandling.Follow,
                    MissingPathHandling.AllowMissingTail,
                    out var resolution,
                    out var physicalFailure))
            {
                return Failure($"Path is unsafe: {physicalFailure.Message}");
            }

            // NOTE: Resolution is a safety snapshot. The lexical target remains the package contract
            // used by later operations and must be resolved again at every physical access boundary.
            return SkillOperationResult<AbsolutePath>.Success(resolution.RequestedPath.Target);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException or PathTooLongException)
        {
            return Failure($"Path is invalid: {exception.Message}");
        }
    }

    /// <summary>Resolves one package-relative path whose final entry is missing or a regular file.</summary>
    internal static SkillOperationResult<AbsolutePath> ResolveRegularFile (
        AbsolutePath packageDirectory,
        PackageRelativePath relativePath)
    {
        ArgumentNullException.ThrowIfNull(packageDirectory);
        ArgumentNullException.ThrowIfNull(relativePath);

        var targetPath = ContainedPath.Create(packageDirectory, relativePath.RootRelativePath).Target;
        if (!FileSystemEntryInspector.TryInspect(targetPath, out var observation, out _)
            || observation.State is not FileSystemEntryState.Missing and not FileSystemEntryState.RegularFile)
        {
            return Failure($"Package file must be a regular file: {relativePath.Value}");
        }

        return ResolveUnderRoot(packageDirectory, targetPath);
    }

    private static SkillOperationResult<AbsolutePath> Failure (string message)
    {
        return SkillOperationResult<AbsolutePath>.FailureResult(SkillFailureCodes.PathUnsafe, message);
    }
}

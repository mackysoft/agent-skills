using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Applies the physical-path safety contract shared by bundle build and publication boundaries. </summary>
internal static class BundleBuildPathGuard
{
    private const string CanonicalOutputDirectoryName = "agent-distribution";

    internal static AgentDistributionOperationResult<AbsolutePath> ValidateSourceRoot (AbsolutePath sourceRoot)
    {
        ArgumentNullException.ThrowIfNull(sourceRoot);

        var resolutionResult = Resolve(sourceRoot, sourceRoot, MissingPathHandling.Reject, "Source bundle root");
        if (!resolutionResult.IsSuccess)
        {
            return Failure<AbsolutePath>(resolutionResult.Failure!.Message);
        }

        return resolutionResult.Value!.TargetObservation.State == FileSystemEntryState.Directory
            ? AgentDistributionOperationResult<AbsolutePath>.Success(sourceRoot)
            : Failure<AbsolutePath>("Source bundle root must be a regular directory.");
    }

    internal static AgentDistributionOperationResult<AbsolutePath> ValidateOutputRoot (AbsolutePath outputRoot)
    {
        ArgumentNullException.ThrowIfNull(outputRoot);

        if (!string.Equals(Path.GetFileName(outputRoot.Value), CanonicalOutputDirectoryName, StringComparison.Ordinal))
        {
            return Failure<AbsolutePath>($"Bundle output root must be named '{CanonicalOutputDirectoryName}': {outputRoot}");
        }

        var resolutionResult = ResolveFromNearestExistingAncestor(outputRoot, "Bundle output root");
        if (!resolutionResult.IsSuccess)
        {
            return Failure<AbsolutePath>(resolutionResult.Failure!.Message);
        }

        return resolutionResult.Value!.TargetObservation.State is FileSystemEntryState.Missing or FileSystemEntryState.Directory
            ? AgentDistributionOperationResult<AbsolutePath>.Success(outputRoot)
            : Failure<AbsolutePath>($"Bundle output root must be missing or a regular directory: {outputRoot}");
    }

    internal static AgentDistributionOperationResult<bool> ValidateDistinctRoots (AbsolutePath sourceRoot, AbsolutePath outputRoot)
    {
        ArgumentNullException.ThrowIfNull(sourceRoot);
        ArgumentNullException.ThrowIfNull(outputRoot);

        var sourceResult = Resolve(sourceRoot, sourceRoot, MissingPathHandling.Reject, "Source bundle root");
        if (!sourceResult.IsSuccess)
        {
            return Failure<bool>(sourceResult.Failure!.Message);
        }

        var outputResult = ResolveFromNearestExistingAncestor(outputRoot, "Bundle output root");
        if (!outputResult.IsSuccess)
        {
            return Failure<bool>(outputResult.Failure!.Message);
        }

        return IsContainedInEitherDirection(sourceResult.Value!.ResolvedPath.Target, outputResult.Value!.ResolvedPath.Target)
            ? Failure<bool>("Source bundle root and bundle output root must not be the same path or contain one another.")
            : AgentDistributionOperationResult<bool>.Success(true);
    }

    /// <summary> Revalidates the parent and publication paths immediately before the directory swap. </summary>
    internal static AgentDistributionOperationResult<bool> ValidatePublicationPaths (
        AbsolutePath outputRoot,
        AbsolutePath stagingRoot,
        AbsolutePath backupRoot)
    {
        var outputResult = ValidateOutputRoot(outputRoot);
        if (!outputResult.IsSuccess)
        {
            return Failure<bool>(outputResult.Failure!.Message);
        }

        if (!outputRoot.TryGetParent(out var parentRoot))
        {
            return Failure<bool>($"Bundle output root parent could not be resolved: {outputRoot}");
        }

        var parentResult = Resolve(parentRoot, parentRoot, MissingPathHandling.Reject, "Bundle output parent");
        if (!parentResult.IsSuccess || parentResult.Value!.TargetObservation.State != FileSystemEntryState.Directory)
        {
            return Failure<bool>(parentResult.IsSuccess ? "Bundle output parent must be a regular directory." : parentResult.Failure!.Message);
        }

        foreach (var path in new[] { stagingRoot, backupRoot })
        {
            var pathResult = Resolve(parentRoot, path, MissingPathHandling.AllowMissingTail, "Bundle publication path");
            if (!pathResult.IsSuccess)
            {
                return Failure<bool>(pathResult.Failure!.Message);
            }

            if (pathResult.Value!.TargetObservation.State is not FileSystemEntryState.Missing and not FileSystemEntryState.Directory)
            {
                return Failure<bool>($"Bundle publication path must be missing or a regular directory: {path}");
            }
        }

        return AgentDistributionOperationResult<bool>.Success(true);
    }

    private static bool IsContainedInEitherDirection (AbsolutePath first, AbsolutePath second)
    {
        return ContainedPath.TryCreate(first, second, out _, out _)
            || ContainedPath.TryCreate(second, first, out _, out _);
    }

    private static AgentDistributionOperationResult<PhysicalPathResolution> Resolve (
        AbsolutePath boundaryRoot,
        AbsolutePath targetPath,
        MissingPathHandling missingPathHandling,
        string description)
    {
        try
        {
            if (!ContainedPath.TryCreate(boundaryRoot, targetPath, out var containedPath, out var containmentFailure))
            {
                return Failure<PhysicalPathResolution>($"{description} is outside its physical validation boundary: {containmentFailure.Message}");
            }

            return PhysicalPathResolver.TryResolve(
                    containedPath,
                    SymbolicLinkHandling.Reject,
                    missingPathHandling,
                    out var resolution,
                    out var failure)
                ? AgentDistributionOperationResult<PhysicalPathResolution>.Success(resolution)
                : Failure<PhysicalPathResolution>($"{description} is unsafe: {failure.Message}");
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return Failure<PhysicalPathResolution>($"{description} could not be inspected: {exception.Message}");
        }
    }

    private static AgentDistributionOperationResult<PhysicalPathResolution> ResolveFromNearestExistingAncestor (
        AbsolutePath targetPath,
        string description)
    {
        var ancestorResult = FindNearestExistingAncestor(targetPath, description);
        return ancestorResult.IsSuccess
            ? Resolve(ancestorResult.Value!, targetPath, MissingPathHandling.AllowMissingTail, description)
            : Failure<PhysicalPathResolution>(ancestorResult.Failure!.Message);
    }

    private static AgentDistributionOperationResult<AbsolutePath> FindNearestExistingAncestor (
        AbsolutePath path,
        string description)
    {
        try
        {
            var current = path;
            while (true)
            {
                if (!FileSystemEntryInspector.TryInspect(current, out var observation, out var failure))
                {
                    return Failure<AbsolutePath>($"{description} could not be inspected: {failure.Message}");
                }

                if (observation.State != FileSystemEntryState.Missing)
                {
                    return AgentDistributionOperationResult<AbsolutePath>.Success(current);
                }

                if (!current.TryGetParent(out current))
                {
                    return Failure<AbsolutePath>($"{description} does not have an existing filesystem ancestor.");
                }
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return Failure<AbsolutePath>($"{description} could not be inspected: {exception.Message}");
        }
    }

    private static AgentDistributionOperationResult<T> Failure<T> (string message)
    {
        return AgentDistributionOperationResult<T>.FailureResult(AgentDistributionFailureCodes.PathUnsafe, message);
    }
}

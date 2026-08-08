using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Shared.FileSystem;

/// <summary> Resolves file-system paths while enforcing one canonical root boundary. </summary>
internal static class SkillPathBoundary
{
    /// <summary> Resolves existing symbolic-link segments and verifies that the target remains under the resolved root. </summary>
    /// <param name="rootPath"> The allowed root path. </param>
    /// <param name="targetPath"> The target path to resolve. </param>
    /// <param name="failureCode"> The failure code owned by the calling boundary. </param>
    /// <param name="pathDescription"> The path description used in boundary failures. </param>
    /// <returns> The canonical target path, or the caller-owned failure when it leaves the root. </returns>
    internal static SkillOperationResult<string> ResolveUnderRoot (
        string rootPath,
        string targetPath,
        SkillFailureCode failureCode,
        string pathDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(failureCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathDescription);

        try
        {
            if (!AbsolutePath.TryParse(Path.GetFullPath(rootPath), out var lexicalRoot, out var rootFailure))
            {
                return Failure(failureCode, pathDescription, rootFailure);
            }

            if (!AbsolutePath.TryParse(Path.GetFullPath(targetPath), out var lexicalTarget, out var targetFailure))
            {
                return Failure(failureCode, pathDescription, targetFailure);
            }

            var resolvedRootText = ResolveExistingPathSegments(lexicalRoot.Value);
            var resolvedTargetText = ResolveExistingPathSegments(lexicalTarget.Value);
            if (!AbsolutePath.TryParse(resolvedRootText, out var resolvedRoot, out var resolvedRootFailure))
            {
                return Failure(failureCode, pathDescription, resolvedRootFailure);
            }

            if (!AbsolutePath.TryParse(resolvedTargetText, out var resolvedTarget, out var resolvedTargetFailure))
            {
                return Failure(failureCode, pathDescription, resolvedTargetFailure);
            }

            if (!ContainedPath.TryCreate(resolvedRoot, resolvedTarget, out var containedPath, out var containmentFailure))
            {
                return Failure(failureCode, pathDescription, containmentFailure);
            }

            return SkillOperationResult<string>.Success(containedPath.Target.Value);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException or PathTooLongException)
        {
            return SkillOperationResult<string>.FailureResult(
                failureCode,
                $"{pathDescription} is invalid: {exception.Message}");
        }
    }

    private static string ResolveExistingPathSegments (string path)
    {
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var visitedPaths = new HashSet<string>(pathComparer);
        var currentPath = Path.GetFullPath(path);
        while (true)
        {
            if (!visitedPaths.Add(currentPath))
            {
                throw new IOException($"Symbolic-link path resolution contains a cycle: {path}");
            }

            var resolvedPath = ResolveExistingPathSegmentsOnce(currentPath);
            if (pathComparer.Equals(currentPath, resolvedPath))
            {
                return resolvedPath;
            }

            currentPath = resolvedPath;
        }
    }

    private static string ResolveExistingPathSegmentsOnce (string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root))
        {
            return path;
        }

        var currentPath = root;
        var relativePath = path[root.Length..];
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < segments.Length; i++)
        {
            currentPath = Path.Combine(currentPath, segments[i]);
            if (!Directory.Exists(currentPath))
            {
                if (i == segments.Length - 1 && File.Exists(currentPath))
                {
                    var file = new FileInfo(currentPath);
                    var resolvedFile = file.ResolveLinkTarget(returnFinalTarget: true);
                    if (resolvedFile is not null)
                    {
                        currentPath = resolvedFile.FullName;
                    }
                }

                continue;
            }

            var directory = new DirectoryInfo(currentPath);
            var resolvedDirectory = directory.ResolveLinkTarget(returnFinalTarget: true);
            if (resolvedDirectory is not null)
            {
                currentPath = resolvedDirectory.FullName;
            }
        }

        return Path.GetFullPath(currentPath);
    }

    private static SkillOperationResult<string> Failure (
        SkillFailureCode failureCode,
        string pathDescription,
        PathValidationFailure failure)
    {
        return SkillOperationResult<string>.FailureResult(
            failureCode,
            $"{pathDescription} is invalid: {failure.Message}");
    }
}

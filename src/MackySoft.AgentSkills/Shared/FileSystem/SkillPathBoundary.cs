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

        var rootFullPath = ResolveExistingPathSegments(Path.GetFullPath(rootPath));
        var targetFullPath = ResolveExistingPathSegments(Path.GetFullPath(targetPath));
        if (!IsUnderOrEqual(rootFullPath, targetFullPath))
        {
            return SkillOperationResult<string>.FailureResult(
                failureCode,
                $"{pathDescription} must stay under root '{rootFullPath}': {targetFullPath}");
        }

        return SkillOperationResult<string>.Success(targetFullPath);
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

    private static bool IsUnderOrEqual (
        string rootPath,
        string targetPath)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var normalizedRoot = EnsureTrailingDirectorySeparator(rootPath);
        var normalizedTarget = EnsureTrailingDirectorySeparator(targetPath);
        return string.Equals(normalizedRoot, normalizedTarget, comparison)
            || normalizedTarget.StartsWith(normalizedRoot, comparison);
    }

    private static string EnsureTrailingDirectorySeparator (string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}

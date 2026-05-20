namespace MackySoft.AgentSkills.Installation.Validation;

/// <summary> Builds installed package directory sets from package-relative file paths. </summary>
internal static class SkillInstalledDirectorySet
{
    /// <summary> Builds the directory set required by package-relative file paths. </summary>
    /// <param name="relativeFilePaths"> The package-relative file paths. </param>
    /// <returns> Directory paths that may exist below the package root. </returns>
    public static HashSet<string> BuildParentDirectories (IEnumerable<string> relativeFilePaths)
    {
        ArgumentNullException.ThrowIfNull(relativeFilePaths);

        var directoryPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var relativeFilePath in relativeFilePaths)
        {
            AddParentDirectories(directoryPaths, relativeFilePath);
        }

        return directoryPaths;
    }

    /// <summary> Adds every parent directory of one package-relative file path. </summary>
    /// <param name="directoryPaths"> The mutable directory set. </param>
    /// <param name="relativeFilePath"> The package-relative file path. </param>
    public static void AddParentDirectories (
        HashSet<string> directoryPaths,
        string relativeFilePath)
    {
        ArgumentNullException.ThrowIfNull(directoryPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeFilePath);

        var normalizedPath = relativeFilePath.Replace('\\', '/');
        var lastSeparatorIndex = normalizedPath.LastIndexOf('/');
        while (lastSeparatorIndex > 0)
        {
            var directoryPath = normalizedPath[..lastSeparatorIndex];
            directoryPaths.Add(directoryPath);
            lastSeparatorIndex = directoryPath.LastIndexOf('/');
        }
    }
}

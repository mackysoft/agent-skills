namespace MackySoft.AgentDistribution.Installation.Validation;

using MackySoft.AgentDistribution.Shared;

/// <summary> Builds installed package directory sets from package-relative file paths. </summary>
internal static class SkillInstalledDirectorySet
{
    /// <summary> Builds the directory set required by package-relative file paths. </summary>
    /// <param name="relativeFilePaths"> The package-relative file paths. </param>
    /// <returns> Directory paths that may exist below the package root. </returns>
    public static HashSet<PackageRelativePath> BuildParentDirectories (IEnumerable<PackageRelativePath> relativeFilePaths)
    {
        ArgumentNullException.ThrowIfNull(relativeFilePaths);

        var directoryPaths = new HashSet<PackageRelativePath>();
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
        HashSet<PackageRelativePath> directoryPaths,
        PackageRelativePath relativeFilePath)
    {
        ArgumentNullException.ThrowIfNull(directoryPaths);
        ArgumentNullException.ThrowIfNull(relativeFilePath);

        var normalizedPath = relativeFilePath.Value;
        var lastSeparatorIndex = normalizedPath.LastIndexOf('/');
        while (lastSeparatorIndex > 0)
        {
            var directoryPath = normalizedPath[..lastSeparatorIndex];
            directoryPaths.Add(PackageRelativePath.Parse(directoryPath));
            lastSeparatorIndex = directoryPath.LastIndexOf('/');
        }
    }
}

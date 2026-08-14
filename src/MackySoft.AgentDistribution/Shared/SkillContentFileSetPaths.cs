namespace MackySoft.AgentDistribution.Shared;

/// <summary> Defines package-relative paths that participate in host-independent SKILL content integrity. </summary>
internal static class SkillContentFileSetPaths
{
    /// <summary> Gets the package-relative path for the canonical SKILL body. </summary>
    public static PackageRelativePath SkillBodyPath { get; } = PackageRelativePath.Parse("SKILL.md");

    /// <summary> Gets the package-relative path prefix for reference files. </summary>
    public static PackageRelativePath ReferencesDirectoryPath { get; } = PackageRelativePath.Parse("references");

    /// <summary> Gets the package-relative path prefix for script files. </summary>
    public static PackageRelativePath ScriptsDirectoryPath { get; } = PackageRelativePath.Parse("scripts");

    /// <summary> Determines whether a package file participates in host-independent SKILL content integrity. </summary>
    /// <param name="relativePath"> The package-relative file path to inspect. </param>
    /// <returns> <see langword="true" /> when the file contributes to SKILL content integrity; otherwise <see langword="false" />. </returns>
    public static bool IsContentFile (PackageRelativePath relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        return relativePath == SkillBodyPath || IsSupplementalContentFile(relativePath);
    }

    /// <summary> Determines whether a supplemental host-independent content file participates in SKILL content integrity. </summary>
    /// <param name="relativePath"> The package-relative file path to inspect. </param>
    /// <returns> <see langword="true" /> when the file is below <c>references</c> or <c>scripts</c>; otherwise <see langword="false" />. </returns>
    public static bool IsSupplementalContentFile (PackageRelativePath relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        return relativePath.IsDescendantOf(ReferencesDirectoryPath)
            || relativePath.IsDescendantOf(ScriptsDirectoryPath);
    }
}

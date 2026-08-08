using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;

namespace MackySoft.AgentSkills.Distribution;

/// <summary> Resolves the transitive SKILL package dependencies for already validated canonical packages. </summary>
internal static class SkillPackageDependencyResolver
{
    /// <summary> Resolves root SKILL packages and every package they transitively depend on. </summary>
    /// <param name="packages"> The complete validated canonical SKILL package collection. </param>
    /// <param name="rootSkillNames"> The validated root SKILL names. </param>
    /// <returns> Distinct resolved packages ordered by SKILL name using ordinal comparison. </returns>
    public static IReadOnlyList<CanonicalSkillPackage> Resolve (
        IReadOnlyList<CanonicalSkillPackage> packages,
        IReadOnlyList<SkillName> rootSkillNames)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(rootSkillNames);

        var packagesBySkillName = packages.ToDictionary(static package => package.Manifest.SkillName);
        var resolvedSkillNames = new HashSet<SkillName>();
        foreach (var skillName in rootSkillNames)
        {
            AddPackageAndDependencies(skillName, packagesBySkillName, resolvedSkillNames);
        }

        return resolvedSkillNames
            .OrderBy(static skillName => skillName.Value, StringComparer.Ordinal)
            .Select(skillName => packagesBySkillName[skillName])
            .ToArray();
    }

    private static void AddPackageAndDependencies (
        SkillName skillName,
        IReadOnlyDictionary<SkillName, CanonicalSkillPackage> packagesBySkillName,
        HashSet<SkillName> resolvedSkillNames)
    {
        if (!resolvedSkillNames.Add(skillName))
        {
            return;
        }

        foreach (var dependency in packagesBySkillName[skillName].Manifest.Dependencies)
        {
            AddPackageAndDependencies(dependency, packagesBySkillName, resolvedSkillNames);
        }
    }
}

using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;

namespace MackySoft.AgentSkills.Distribution;

/// <summary> Represents one validated bundled SKILL package inventory and the caller's root selection. </summary>
public sealed class SkillPackageCatalog
{
    /// <summary> Initializes one immutable package catalog snapshot. </summary>
    /// <param name="bundleDescriptor"> The descriptor that owns the complete bundled package set. </param>
    /// <param name="selectedCategories"> The caller's product-owned root category selection. </param>
    /// <param name="selectedSkillNames"> The caller's exact root SKILL name selection. Empty means no name filter. </param>
    /// <param name="availableCategories"> The categories present in the complete bundle with package counts. </param>
    /// <param name="packages"> The selected root packages and their transitive dependencies. </param>
    /// <exception cref="ArgumentNullException"> Thrown when the descriptor or a collection is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a collection contains <see langword="null" />, available categories contain duplicate categories,
    /// package names are not unique, or a package does not belong to the descriptor's catalog and bundle version.
    /// </exception>
    internal SkillPackageCatalog (
        SkillBundleDescriptor bundleDescriptor,
        IReadOnlyList<SkillCategory> selectedCategories,
        IReadOnlyList<SkillName> selectedSkillNames,
        IReadOnlyList<SkillCategoryPackageCount> availableCategories,
        IReadOnlyList<CanonicalSkillPackage> packages)
    {
        BundleDescriptor = bundleDescriptor ?? throw new ArgumentNullException(nameof(bundleDescriptor));
        SelectedCategories = CopyRequiredItems(selectedCategories, nameof(selectedCategories));
        SelectedSkillNames = CopyRequiredItems(selectedSkillNames, nameof(selectedSkillNames));
        AvailableCategories = CreateAvailableCategorySnapshot(availableCategories, nameof(availableCategories));
        Packages = CreatePackageSnapshot(packages, bundleDescriptor, nameof(packages));
    }

    /// <summary> Gets the descriptor that owns the complete bundled package set. </summary>
    public SkillBundleDescriptor BundleDescriptor { get; }

    /// <summary> Gets the caller's product-owned root category selection. </summary>
    public IReadOnlyList<SkillCategory> SelectedCategories { get; }

    /// <summary> Gets the caller's exact root SKILL name selection. Empty means no name filter. </summary>
    public IReadOnlyList<SkillName> SelectedSkillNames { get; }

    /// <summary> Gets the categories present in the complete bundle with package counts in ordinal order. </summary>
    public IReadOnlyList<SkillCategoryPackageCount> AvailableCategories { get; }

    /// <summary> Gets the selected root packages and their transitive dependencies, sorted by skill name using ordinal comparison. </summary>
    public IReadOnlyList<CanonicalSkillPackage> Packages { get; }

    private static IReadOnlyList<T> CopyRequiredItems<T> (
        IReadOnlyList<T> items,
        string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(items, parameterName);

        var snapshot = new List<T>(items.Count);
        foreach (var item in items)
        {
            if (item is null)
            {
                throw new ArgumentException("Catalog collections must not contain null items.", parameterName);
            }

            snapshot.Add(item);
        }

        return Array.AsReadOnly(snapshot.ToArray());
    }

    private static IReadOnlyList<SkillCategoryPackageCount> CreateAvailableCategorySnapshot (
        IReadOnlyList<SkillCategoryPackageCount> availableCategories,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(availableCategories, parameterName);

        var snapshot = new List<SkillCategoryPackageCount>(availableCategories.Count);
        var categories = new HashSet<SkillCategory>();
        foreach (var categoryPackageCount in availableCategories)
        {
            if (categoryPackageCount is null)
            {
                throw new ArgumentException("Available categories must not contain null items.", parameterName);
            }

            if (!categories.Add(categoryPackageCount.Category))
            {
                throw new ArgumentException(
                    $"Available categories must contain unique categories: {categoryPackageCount.Category.Value}",
                    parameterName);
            }

            snapshot.Add(categoryPackageCount);
        }

        return Array.AsReadOnly(snapshot
            .OrderBy(static item => item.Category.Value, StringComparer.Ordinal)
            .ToArray());
    }

    private static IReadOnlyList<CanonicalSkillPackage> CreatePackageSnapshot (
        IReadOnlyList<CanonicalSkillPackage> packages,
        SkillBundleDescriptor bundleDescriptor,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(packages, parameterName);

        var snapshot = new List<CanonicalSkillPackage>(packages.Count);
        var skillNames = new HashSet<SkillName>();
        foreach (var package in packages)
        {
            if (package is null)
            {
                throw new ArgumentException("Catalog packages must not contain null items.", parameterName);
            }

            if (package.Manifest.CatalogId != bundleDescriptor.CatalogId)
            {
                throw new ArgumentException(
                    $"Catalog package belongs to another catalog: {package.Manifest.SkillName.Value}",
                    parameterName);
            }

            if (package.Manifest.SkillBundleVersion != bundleDescriptor.SkillBundleVersion)
            {
                throw new ArgumentException(
                    $"Catalog package belongs to another SKILL bundle version: {package.Manifest.SkillName.Value}",
                    parameterName);
            }

            if (!skillNames.Add(package.Manifest.SkillName))
            {
                throw new ArgumentException(
                    $"Catalog packages must contain unique SKILL names: {package.Manifest.SkillName.Value}",
                    parameterName);
            }

            snapshot.Add(package);
        }

        return Array.AsReadOnly(snapshot
            .OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal)
            .ToArray());
    }
}

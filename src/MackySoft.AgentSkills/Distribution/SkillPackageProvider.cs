using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Categories;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Selection;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Distribution;

/// <summary> Provides selected SKILL packages from one validated generated bundle. </summary>
public sealed class SkillPackageProvider
{
    private readonly BundledSkillPackageRootResolver packageRootResolver;
    private readonly CanonicalSkillBundleReader bundleReader;

    /// <summary> Initializes a new instance of the <see cref="SkillPackageProvider" /> class. </summary>
    /// <param name="packageRootResolver"> The bundled generated SKILL package root resolver. </param>
    /// <param name="bundleReader"> The canonical bundle reader. </param>
    public SkillPackageProvider (
        BundledSkillPackageRootResolver packageRootResolver,
        CanonicalSkillBundleReader bundleReader)
    {
        this.packageRootResolver = packageRootResolver ?? throw new ArgumentNullException(nameof(packageRootResolver));
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
    }

    /// <summary> Gets every package from the validated bundled SKILL package set. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The canonical packages, or a bundle-resolution failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> GetPackagesAsync (
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bundleResult = await ReadBundleAsync(cancellationToken).ConfigureAwait(false);
        return bundleResult.IsSuccess
            ? SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.Success(bundleResult.Value!.Packages)
            : SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                bundleResult.Failure!.Code,
                bundleResult.Failure.Message);
    }

    /// <summary> Gets the complete validated bundled package catalog. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The complete package catalog, or a bundle-resolution failure. </returns>
    public ValueTask<SkillOperationResult<SkillPackageCatalog>> GetPackageCatalogAsync (
        CancellationToken cancellationToken = default)
    {
        return GetPackageCatalogAsync([], [], cancellationToken);
    }

    /// <summary> Gets packages selected by exact category and SKILL-name literals. </summary>
    /// <param name="selectedCategoryLiterals"> The selected category literals. Empty selects every category present in the bundle. </param>
    /// <param name="selectedSkillNames"> The exact selected SKILL names. Empty disables the name filter. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The selected root packages and their transitive dependencies, or a selection or bundle failure. </returns>
    public async ValueTask<SkillOperationResult<SkillPackageCatalog>> GetPackageCatalogAsync (
        IReadOnlyList<string> selectedCategoryLiterals,
        IReadOnlyList<string> selectedSkillNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedCategoryLiterals);
        ArgumentNullException.ThrowIfNull(selectedSkillNames);
        cancellationToken.ThrowIfCancellationRequested();

        var skillNameSelectionResult = SkillNameLiteralParser.ParseOptionalSkillNames(selectedSkillNames);
        if (!skillNameSelectionResult.IsSuccess)
        {
            return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                skillNameSelectionResult.Failure!.Code,
                skillNameSelectionResult.Failure.Message);
        }

        var bundleResult = await ReadBundleAsync(cancellationToken).ConfigureAwait(false);
        if (!bundleResult.IsSuccess)
        {
            return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                bundleResult.Failure!.Code,
                bundleResult.Failure.Message);
        }

        return CreatePackageCatalog(
            bundleResult.Value!,
            selectedCategoryLiterals,
            skillNameSelectionResult.Value!);
    }

    /// <summary> Gets packages selected by exact category literals. </summary>
    /// <param name="selectedCategoryLiterals"> The selected category literals. Empty selects every category present in the bundle. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The selected root packages and their transitive dependencies, or a selection or bundle failure. </returns>
    public ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> GetPackagesAsync (
        IReadOnlyList<string> selectedCategoryLiterals,
        CancellationToken cancellationToken = default)
    {
        return GetPackagesAsync(selectedCategoryLiterals, [], cancellationToken);
    }

    /// <summary> Gets packages selected by exact category and SKILL-name literals. </summary>
    /// <param name="selectedCategoryLiterals"> The selected category literals. Empty selects every category present in the bundle. </param>
    /// <param name="selectedSkillNames"> The exact selected SKILL names. Empty disables the name filter. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The selected root packages and their transitive dependencies, or a selection or bundle failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> GetPackagesAsync (
        IReadOnlyList<string> selectedCategoryLiterals,
        IReadOnlyList<string> selectedSkillNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedCategoryLiterals);
        ArgumentNullException.ThrowIfNull(selectedSkillNames);
        cancellationToken.ThrowIfCancellationRequested();

        var catalogResult = await GetPackageCatalogAsync(selectedCategoryLiterals, selectedSkillNames, cancellationToken).ConfigureAwait(false);
        return catalogResult.IsSuccess
            ? SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.Success(catalogResult.Value!.Packages)
            : SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                catalogResult.Failure!.Code,
                catalogResult.Failure.Message);
    }

    /// <summary> Gets packages selected by exact SKILL names across every category. </summary>
    /// <param name="selectedSkillNames"> The exact selected SKILL names. Empty disables the name filter. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The selected root packages and their transitive dependencies, or a selection or bundle failure. </returns>
    public ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> GetPackagesBySkillNamesAsync (
        IReadOnlyList<string> selectedSkillNames,
        CancellationToken cancellationToken = default)
    {
        return GetPackagesAsync([], selectedSkillNames, cancellationToken);
    }

    /// <summary> Gets a catalog selected by exact SKILL names across every category. </summary>
    /// <param name="selectedSkillNames"> The exact selected SKILL names. Empty disables the name filter. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The selected package catalog, or a selection or bundle failure. </returns>
    public ValueTask<SkillOperationResult<SkillPackageCatalog>> GetPackageCatalogBySkillNamesAsync (
        IReadOnlyList<string> selectedSkillNames,
        CancellationToken cancellationToken = default)
    {
        return GetPackageCatalogAsync([], selectedSkillNames, cancellationToken);
    }

    private async ValueTask<SkillOperationResult<CanonicalSkillBundle>> ReadBundleAsync (CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string packageRoot;
        try
        {
            packageRoot = packageRootResolver.Resolve();
        }
        catch (DirectoryNotFoundException ex)
        {
            return SkillOperationResult<CanonicalSkillBundle>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                ex.Message);
        }

        return await bundleReader.ReadAsync(packageRoot, cancellationToken).ConfigureAwait(false);
    }

    private static SkillOperationResult<SkillPackageCatalog> CreatePackageCatalog (
        CanonicalSkillBundle bundle,
        IReadOnlyList<string> selectedCategoryLiterals,
        IReadOnlyList<SkillName> selectedSkillNames)
    {
        var availableCategories = bundle.Packages
            .GroupBy(static package => package.Manifest.Category)
            .OrderBy(static group => group.Key.Value, StringComparer.Ordinal)
            .Select(static group => new SkillCategoryPackageCount(group.Key, group.Count()))
            .ToArray();
        var availableCategoryValues = availableCategories
            .Select(static item => item.Category)
            .ToArray();

        IReadOnlyList<SkillCategory> selectedCategories;
        if (selectedCategoryLiterals.Count == 0)
        {
            selectedCategories = availableCategoryValues;
        }
        else
        {
            var categorySelectionResult = SkillCategoryLiteralParser.ParseSelectedCategories(
                availableCategoryValues,
                selectedCategoryLiterals);
            if (!categorySelectionResult.IsSuccess)
            {
                return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                    categorySelectionResult.Failure!.Code,
                    categorySelectionResult.Failure.Message);
            }

            selectedCategories = categorySelectionResult.Value!;
        }

        var packageIndex = bundle.Packages.ToDictionary(static package => package.Manifest.SkillName);
        var selectedCategorySet = selectedCategories.ToHashSet();
        foreach (var skillName in selectedSkillNames)
        {
            if (!packageIndex.TryGetValue(skillName, out var package))
            {
                return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"Selected SKILL name was not found: {skillName.Value}.");
            }

            if (!selectedCategorySet.Contains(package.Manifest.Category))
            {
                return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"Selected SKILL name '{skillName.Value}' does not match selected categories: {string.Join(", ", selectedCategories.Select(static category => category.Value))}. Its category is: {package.Manifest.Category.Value}.");
            }
        }

        var selectedSkillNameSet = selectedSkillNames.ToHashSet();
        var rootPackages = bundle.Packages
            .Where(package => selectedCategorySet.Contains(package.Manifest.Category))
            .Where(package => selectedSkillNameSet.Count == 0 || selectedSkillNameSet.Contains(package.Manifest.SkillName))
            .OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal)
            .ToArray();
        var resolvedPackages = ResolveDependencyClosure(rootPackages, packageIndex);

        return SkillOperationResult<SkillPackageCatalog>.Success(new SkillPackageCatalog(
            bundle.Descriptor,
            selectedCategories,
            selectedSkillNames,
            availableCategories,
            resolvedPackages));
    }

    private static IReadOnlyList<CanonicalSkillPackage> ResolveDependencyClosure (
        IReadOnlyList<CanonicalSkillPackage> rootPackages,
        IReadOnlyDictionary<SkillName, CanonicalSkillPackage> packagesBySkillName)
    {
        var resolvedSkillNames = new HashSet<SkillName>();
        foreach (var package in rootPackages)
        {
            AddPackageAndDependencies(package.Manifest.SkillName, packagesBySkillName, resolvedSkillNames);
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

        foreach (var dependency in packagesBySkillName[skillName].Manifest.Dependencies.OrderBy(static item => item.Value, StringComparer.Ordinal))
        {
            AddPackageAndDependencies(dependency, packagesBySkillName, resolvedSkillNames);
        }
    }
}

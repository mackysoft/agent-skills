using MackySoft.AgentSkills.Dependencies;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Selection;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Tiers;

namespace MackySoft.AgentSkills.Distribution;

/// <summary> Provides SKILL packages from bundled generated package files. </summary>
public sealed class SkillPackageProvider
{
    private readonly BundledSkillPackageRootResolver packageRootResolver;
    private readonly CanonicalSkillPackageReader packageReader;

    /// <summary> Initializes a new instance of the <see cref="SkillPackageProvider" /> class. </summary>
    /// <param name="packageRootResolver"> The bundled generated SKILL package root resolver. </param>
    /// <param name="packageReader"> The canonical package reader. </param>
    public SkillPackageProvider (
        BundledSkillPackageRootResolver packageRootResolver,
        CanonicalSkillPackageReader packageReader)
    {
        this.packageRootResolver = packageRootResolver ?? throw new ArgumentNullException(nameof(packageRootResolver));
        this.packageReader = packageReader ?? throw new ArgumentNullException(nameof(packageReader));
    }

    /// <summary> Gets all SKILL packages from bundled generated package files. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The canonical packages, or a package-resolution failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> GetPackagesAsync (
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string packageRoot;
        try
        {
            packageRoot = packageRootResolver.Resolve();
        }
        catch (DirectoryNotFoundException ex)
        {
            return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                ex.Message);
        }

        var packagesResult = await packageReader.ReadAllAsync(packageRoot, cancellationToken).ConfigureAwait(false);
        if (!packagesResult.IsSuccess)
        {
            return packagesResult;
        }

        var packageIndexResult = CreatePackageIndex(packagesResult.Value!);
        if (!packageIndexResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                packageIndexResult.Failure!.Code,
                packageIndexResult.Failure.Message);
        }

        return packagesResult;
    }

    /// <summary> Gets bundled SKILL package inventory validated against product-owned tier literals. </summary>
    /// <param name="definedTierLiterals"> The complete product-owned tier literals. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The validated package catalog, or a package-resolution failure. </returns>
    public async ValueTask<SkillOperationResult<SkillPackageCatalog>> GetPackageCatalogAsync (
        IReadOnlyList<string> definedTierLiterals,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definedTierLiterals);
        cancellationToken.ThrowIfCancellationRequested();

        var definedTiersResult = SkillTierLiteralParser.ParseDefinedTiers(definedTierLiterals);
        if (!definedTiersResult.IsSuccess)
        {
            return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                definedTiersResult.Failure!.Code,
                definedTiersResult.Failure.Message);
        }

        return await GetPackageCatalogAsync(definedTiersResult.Value!, definedTiersResult.Value!, [], cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Gets bundled SKILL package inventory and selected packages validated against exact SKILL names. </summary>
    /// <param name="definedTierLiterals"> The complete product-owned tier literals. </param>
    /// <param name="selectedSkillNames"> The exact SKILL names selected by the caller. Empty means no name filter. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The validated package catalog whose packages contain the selected root packages and their transitive dependencies, or a package-resolution failure. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="definedTierLiterals" /> or <paramref name="selectedSkillNames" /> is <see langword="null" />. </exception>
    public async ValueTask<SkillOperationResult<SkillPackageCatalog>> GetPackageCatalogBySkillNamesAsync (
        IReadOnlyList<string> definedTierLiterals,
        IReadOnlyList<string> selectedSkillNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definedTierLiterals);
        ArgumentNullException.ThrowIfNull(selectedSkillNames);
        cancellationToken.ThrowIfCancellationRequested();

        var definedTiersResult = SkillTierLiteralParser.ParseDefinedTiers(definedTierLiterals);
        if (!definedTiersResult.IsSuccess)
        {
            return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                definedTiersResult.Failure!.Code,
                definedTiersResult.Failure.Message);
        }

        return await GetPackageCatalogAsync(definedTiersResult.Value!, definedTiersResult.Value!, selectedSkillNames, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Gets bundled SKILL package inventory and selected packages validated against product-owned tier literals. </summary>
    /// <param name="definedTierLiterals"> The complete product-owned tier literals. </param>
    /// <param name="selectedTiers"> The normalized selected product-owned SKILL tiers. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The validated package catalog whose packages contain the selected root packages and their transitive dependencies, or a package-resolution failure. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="definedTierLiterals" />, <paramref name="selectedTiers" />, or an item in <paramref name="selectedTiers" /> is <see langword="null" />. </exception>
    public async ValueTask<SkillOperationResult<SkillPackageCatalog>> GetPackageCatalogAsync (
        IReadOnlyList<string> definedTierLiterals,
        IReadOnlyList<SkillTier> selectedTiers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definedTierLiterals);
        ArgumentNullException.ThrowIfNull(selectedTiers);
        cancellationToken.ThrowIfCancellationRequested();

        var definedTiersResult = SkillTierLiteralParser.ParseDefinedTiers(definedTierLiterals);
        if (!definedTiersResult.IsSuccess)
        {
            return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                definedTiersResult.Failure!.Code,
                definedTiersResult.Failure.Message);
        }

        var selectionResult = ValidateSelectedTiers(definedTiersResult.Value!, selectedTiers);
        if (!selectionResult.IsSuccess)
        {
            return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                selectionResult.Failure!.Code,
                selectionResult.Failure.Message);
        }

        return await GetPackageCatalogAsync(definedTiersResult.Value!, selectionResult.Value!, [], cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Gets bundled SKILL package inventory and selected packages validated against product-owned tier literals and exact SKILL names. </summary>
    /// <param name="definedTierLiterals"> The complete product-owned tier literals. </param>
    /// <param name="selectedTiers"> The normalized selected product-owned SKILL tiers. </param>
    /// <param name="selectedSkillNames"> The exact SKILL names selected by the caller. Empty means no name filter. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The validated package catalog whose packages contain the selected root packages and their transitive dependencies, or a package-resolution failure. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="definedTierLiterals" />, <paramref name="selectedTiers" />, <paramref name="selectedSkillNames" />, or an item in <paramref name="selectedTiers" /> is <see langword="null" />. </exception>
    public async ValueTask<SkillOperationResult<SkillPackageCatalog>> GetPackageCatalogAsync (
        IReadOnlyList<string> definedTierLiterals,
        IReadOnlyList<SkillTier> selectedTiers,
        IReadOnlyList<string> selectedSkillNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definedTierLiterals);
        ArgumentNullException.ThrowIfNull(selectedTiers);
        ArgumentNullException.ThrowIfNull(selectedSkillNames);
        cancellationToken.ThrowIfCancellationRequested();

        var definedTiersResult = SkillTierLiteralParser.ParseDefinedTiers(definedTierLiterals);
        if (!definedTiersResult.IsSuccess)
        {
            return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                definedTiersResult.Failure!.Code,
                definedTiersResult.Failure.Message);
        }

        var tierSelectionResult = ValidateSelectedTiers(definedTiersResult.Value!, selectedTiers);
        if (!tierSelectionResult.IsSuccess)
        {
            return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                tierSelectionResult.Failure!.Code,
                tierSelectionResult.Failure.Message);
        }

        return await GetPackageCatalogAsync(definedTiersResult.Value!, tierSelectionResult.Value!, selectedSkillNames, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Gets bundled SKILL packages that exactly match selected product-owned tier literals. </summary>
    /// <param name="definedTierLiterals"> The complete product-owned tier literals. </param>
    /// <param name="selectedTierLiterals"> The selected product-owned SKILL tier literals. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The selected root packages and their transitive dependencies, an empty list when the selected tiers are valid but have no packages, or a package-resolution failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> GetPackagesAsync (
        IReadOnlyList<string> definedTierLiterals,
        IReadOnlyList<string> selectedTierLiterals,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definedTierLiterals);
        ArgumentNullException.ThrowIfNull(selectedTierLiterals);

        var selectionResult = SkillTierLiteralParser.ParseSelectedTiers(definedTierLiterals, selectedTierLiterals);
        if (!selectionResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                selectionResult.Failure!.Code,
                selectionResult.Failure.Message);
        }

        return await GetPackagesAsync(definedTierLiterals, selectionResult.Value!, [], cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Gets bundled SKILL packages that exactly match selected product-owned tier literals and exact SKILL names. </summary>
    /// <param name="definedTierLiterals"> The complete product-owned tier literals. </param>
    /// <param name="selectedTierLiterals"> The selected product-owned SKILL tier literals. </param>
    /// <param name="selectedSkillNames"> The exact SKILL names selected by the caller. Empty means no name filter. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The selected root packages and their transitive dependencies, an empty list when the selected tiers are valid but have no packages, or a package-resolution failure. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="definedTierLiterals" />, <paramref name="selectedTierLiterals" />, or <paramref name="selectedSkillNames" /> is <see langword="null" />. </exception>
    public async ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> GetPackagesAsync (
        IReadOnlyList<string> definedTierLiterals,
        IReadOnlyList<string> selectedTierLiterals,
        IReadOnlyList<string> selectedSkillNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definedTierLiterals);
        ArgumentNullException.ThrowIfNull(selectedTierLiterals);
        ArgumentNullException.ThrowIfNull(selectedSkillNames);

        var selectionResult = SkillTierLiteralParser.ParseSelectedTiers(definedTierLiterals, selectedTierLiterals);
        if (!selectionResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                selectionResult.Failure!.Code,
                selectionResult.Failure.Message);
        }

        return await GetPackagesAsync(definedTierLiterals, selectionResult.Value!, selectedSkillNames, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Gets bundled SKILL packages that exactly match selected SKILL names across all defined tiers. </summary>
    /// <param name="definedTierLiterals"> The complete product-owned tier literals. </param>
    /// <param name="selectedSkillNames"> The exact SKILL names selected by the caller. Empty means no name filter. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The selected root packages and their transitive dependencies, or a package-resolution failure. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="definedTierLiterals" /> or <paramref name="selectedSkillNames" /> is <see langword="null" />. </exception>
    public async ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> GetPackagesBySkillNamesAsync (
        IReadOnlyList<string> definedTierLiterals,
        IReadOnlyList<string> selectedSkillNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definedTierLiterals);
        ArgumentNullException.ThrowIfNull(selectedSkillNames);
        cancellationToken.ThrowIfCancellationRequested();

        var catalogResult = await GetPackageCatalogBySkillNamesAsync(definedTierLiterals, selectedSkillNames, cancellationToken).ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                catalogResult.Failure!.Code,
                catalogResult.Failure.Message);
        }

        return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.Success(catalogResult.Value!.Packages);
    }

    /// <summary> Gets bundled SKILL packages that exactly match normalized selected product-owned SKILL tiers. </summary>
    /// <param name="definedTierLiterals"> The complete product-owned tier literals. </param>
    /// <param name="selectedTiers"> The normalized selected product-owned SKILL tiers. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The selected root packages and their transitive dependencies, an empty list when the selected tiers are valid but have no packages, or a package-resolution failure. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="definedTierLiterals" />, <paramref name="selectedTiers" />, or an item in <paramref name="selectedTiers" /> is <see langword="null" />. </exception>
    public async ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> GetPackagesAsync (
        IReadOnlyList<string> definedTierLiterals,
        IReadOnlyList<SkillTier> selectedTiers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definedTierLiterals);
        ArgumentNullException.ThrowIfNull(selectedTiers);
        cancellationToken.ThrowIfCancellationRequested();

        var definedTiersResult = SkillTierLiteralParser.ParseDefinedTiers(definedTierLiterals);
        if (!definedTiersResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                definedTiersResult.Failure!.Code,
                definedTiersResult.Failure.Message);
        }

        var selectionResult = ValidateSelectedTiers(definedTiersResult.Value!, selectedTiers);
        if (!selectionResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                selectionResult.Failure!.Code,
                selectionResult.Failure.Message);
        }

        return await GetPackagesAsync(definedTierLiterals, selectionResult.Value!, [], cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Gets bundled SKILL packages that exactly match normalized selected product-owned SKILL tiers and exact SKILL names. </summary>
    /// <param name="definedTierLiterals"> The complete product-owned tier literals. </param>
    /// <param name="selectedTiers"> The normalized selected product-owned SKILL tiers. </param>
    /// <param name="selectedSkillNames"> The exact SKILL names selected by the caller. Empty means no name filter. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The selected root packages and their transitive dependencies, an empty list when the selected tiers are valid but have no packages, or a package-resolution failure. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="definedTierLiterals" />, <paramref name="selectedTiers" />, <paramref name="selectedSkillNames" />, or an item in <paramref name="selectedTiers" /> is <see langword="null" />. </exception>
    public async ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> GetPackagesAsync (
        IReadOnlyList<string> definedTierLiterals,
        IReadOnlyList<SkillTier> selectedTiers,
        IReadOnlyList<string> selectedSkillNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definedTierLiterals);
        ArgumentNullException.ThrowIfNull(selectedTiers);
        ArgumentNullException.ThrowIfNull(selectedSkillNames);
        cancellationToken.ThrowIfCancellationRequested();

        var catalogResult = await GetPackageCatalogAsync(definedTierLiterals, selectedTiers, selectedSkillNames, cancellationToken).ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                catalogResult.Failure!.Code,
                catalogResult.Failure.Message);
        }

        return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.Success(catalogResult.Value!.Packages);
    }

    private async ValueTask<SkillOperationResult<SkillPackageCatalog>> GetPackageCatalogAsync (
        IReadOnlyList<SkillTier> definedTiers,
        IReadOnlyList<SkillTier> selectedTiers,
        IReadOnlyList<string> selectedSkillNames,
        CancellationToken cancellationToken)
    {
        var skillNameSelectionResult = SkillNameLiteralParser.ParseOptionalSkillNames(selectedSkillNames);
        if (!skillNameSelectionResult.IsSuccess)
        {
            return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                skillNameSelectionResult.Failure!.Code,
                skillNameSelectionResult.Failure.Message);
        }

        var packagesResult = await GetPackagesAsync(cancellationToken).ConfigureAwait(false);
        if (!packagesResult.IsSuccess)
        {
            return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                packagesResult.Failure!.Code,
                packagesResult.Failure.Message);
        }

        return CreatePackageCatalog(definedTiers, selectedTiers, skillNameSelectionResult.Value!, packagesResult.Value!);
    }

    private static SkillOperationResult<SkillPackageCatalog> CreatePackageCatalog (
        IReadOnlyList<SkillTier> definedTiers,
        IReadOnlyList<SkillTier> selectedTiers,
        IReadOnlyList<SkillName> selectedSkillNames,
        IReadOnlyList<CanonicalSkillPackage> packages)
    {
        var definedTierSet = definedTiers.ToHashSet();
        var selectedTierSet = selectedTiers.ToHashSet();
        var selectedSkillNameSet = selectedSkillNames.ToHashSet();
        var counts = definedTiers.ToDictionary(static tier => tier, static _ => 0);
        var packageIndexResult = CreatePackageIndex(packages);
        if (!packageIndexResult.IsSuccess)
        {
            return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                packageIndexResult.Failure!.Code,
                packageIndexResult.Failure.Message);
        }

        var packagesBySkillName = packageIndexResult.Value!;
        foreach (var package in packages)
        {
            if (!definedTierSet.Contains(package.Manifest.Tier))
            {
                return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                    SkillFailureCodes.ManifestInvalid,
                    $"Generated SKILL package has an undefined tier: {package.Manifest.SkillName.Value}/{package.Manifest.Tier.Value}");
            }

            counts[package.Manifest.Tier]++;
        }

        foreach (var skillName in selectedSkillNames)
        {
            if (!packagesBySkillName.TryGetValue(skillName, out var package))
            {
                return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"Selected SKILL name was not found: {skillName.Value}.");
            }

            if (!selectedTierSet.Contains(package.Manifest.Tier))
            {
                return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"Selected SKILL name '{skillName.Value}' does not match selected tiers: {string.Join(", ", selectedTiers.Select(static tier => tier.Value))}. Its tier is: {package.Manifest.Tier.Value}.");
            }
        }

        var rootPackages = packages
            .Where(package => selectedTierSet.Contains(package.Manifest.Tier))
            .Where(package => selectedSkillNameSet.Count == 0 || selectedSkillNameSet.Contains(package.Manifest.SkillName))
            .OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal)
            .ToArray();
        var resolvedPackages = ResolveDependencyClosure(rootPackages, packagesBySkillName);
        var availableTiers = definedTiers
            .Select(tier => new SkillTierPackageCount(tier, counts[tier]))
            .ToArray();

        return SkillOperationResult<SkillPackageCatalog>.Success(new SkillPackageCatalog(selectedTiers, selectedSkillNames, availableTiers, resolvedPackages));
    }

    private static SkillOperationResult<Dictionary<SkillName, CanonicalSkillPackage>> CreatePackageIndex (IReadOnlyList<CanonicalSkillPackage> packages)
    {
        var packagesBySkillName = new Dictionary<SkillName, CanonicalSkillPackage>();
        foreach (var package in packages)
        {
            if (!packagesBySkillName.TryAdd(package.Manifest.SkillName, package))
            {
                return SkillOperationResult<Dictionary<SkillName, CanonicalSkillPackage>>.FailureResult(
                    SkillFailureCodes.ManifestInvalid,
                    $"Generated SKILL package has a duplicate skillName: {package.Manifest.SkillName.Value}");
            }
        }

        var dependencyGraphResult = SkillDependencyGraphValidator.Validate(
            packagesBySkillName.ToDictionary(
                static item => item.Key,
                static item => item.Value.Manifest.Dependencies),
            SkillFailureCodes.ManifestInvalid,
            "Generated SKILL package");
        if (!dependencyGraphResult.IsSuccess)
        {
            return SkillOperationResult<Dictionary<SkillName, CanonicalSkillPackage>>.FailureResult(
                dependencyGraphResult.Failure!.Code,
                dependencyGraphResult.Failure.Message);
        }

        return SkillOperationResult<Dictionary<SkillName, CanonicalSkillPackage>>.Success(packagesBySkillName);
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

        foreach (var dependency in packagesBySkillName[skillName].Manifest.Dependencies.OrderBy(static dependency => dependency.Value, StringComparer.Ordinal))
        {
            AddPackageAndDependencies(dependency, packagesBySkillName, resolvedSkillNames);
        }
    }

    private static SkillOperationResult<IReadOnlyList<SkillTier>> ValidateSelectedTiers (
        IReadOnlyList<SkillTier> definedTiers,
        IReadOnlyList<SkillTier> selectedTiers)
    {
        if (selectedTiers.Count == 0)
        {
            return SkillOperationResult<IReadOnlyList<SkillTier>>.FailureResult(
                SkillFailureCodes.InputInvalid,
                "At least one SKILL tier must be selected.");
        }

        var definedTierSet = definedTiers.ToHashSet();
        var normalizedSelectedTiers = new List<SkillTier>(selectedTiers.Count);
        var selectedTierSet = new HashSet<SkillTier>();
        foreach (var selectedTier in selectedTiers)
        {
            ArgumentNullException.ThrowIfNull(selectedTier);

            if (!definedTierSet.Contains(selectedTier))
            {
                return SkillOperationResult<IReadOnlyList<SkillTier>>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"Unsupported SKILL tier: {selectedTier.Value}. Supported tiers: {string.Join(", ", definedTiers.Select(static tier => tier.Value))}.");
            }

            if (selectedTierSet.Add(selectedTier))
            {
                normalizedSelectedTiers.Add(selectedTier);
            }
        }

        return SkillOperationResult<IReadOnlyList<SkillTier>>.Success(normalizedSelectedTiers);
    }
}

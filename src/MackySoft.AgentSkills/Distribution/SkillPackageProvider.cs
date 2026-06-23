using MackySoft.AgentSkills.Packaging.Canonical;
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

        return await packageReader.ReadAllAsync(packageRoot, cancellationToken).ConfigureAwait(false);
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

        return await GetPackageCatalogAsync(definedTiersResult.Value!, definedTiersResult.Value!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Gets bundled SKILL package inventory and selected packages validated against product-owned tier literals. </summary>
    /// <param name="definedTierLiterals"> The complete product-owned tier literals. </param>
    /// <param name="selectedTiers"> The normalized selected product-owned SKILL tiers. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The validated package catalog whose packages match <paramref name="selectedTiers" />, or a package-resolution failure. </returns>
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

        return await GetPackageCatalogAsync(definedTiersResult.Value!, selectionResult.Value!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Gets bundled SKILL packages that exactly match selected product-owned tier literals. </summary>
    /// <param name="definedTierLiterals"> The complete product-owned tier literals. </param>
    /// <param name="selectedTierLiterals"> The selected product-owned SKILL tier literals. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The matching canonical packages, an empty list when the selected tiers are valid but have no packages, or a package-resolution failure. </returns>
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

        return await GetPackagesAsync(definedTierLiterals, selectionResult.Value!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Gets bundled SKILL packages that exactly match normalized selected product-owned SKILL tiers. </summary>
    /// <param name="definedTierLiterals"> The complete product-owned tier literals. </param>
    /// <param name="selectedTiers"> The normalized selected product-owned SKILL tiers. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The matching canonical packages, an empty list when the selected tiers are valid but have no packages, or a package-resolution failure. </returns>
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

        var catalogResult = await GetPackageCatalogAsync(definedTiersResult.Value!, selectionResult.Value!, cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken)
    {
        var packagesResult = await GetPackagesAsync(cancellationToken).ConfigureAwait(false);
        if (!packagesResult.IsSuccess)
        {
            return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                packagesResult.Failure!.Code,
                packagesResult.Failure.Message);
        }

        return CreatePackageCatalog(definedTiers, selectedTiers, packagesResult.Value!);
    }

    private static SkillOperationResult<SkillPackageCatalog> CreatePackageCatalog (
        IReadOnlyList<SkillTier> definedTiers,
        IReadOnlyList<SkillTier> selectedTiers,
        IReadOnlyList<CanonicalSkillPackage> packages)
    {
        var definedTierSet = definedTiers.ToHashSet();
        var selectedTierSet = selectedTiers.ToHashSet();
        var counts = definedTiers.ToDictionary(static tier => tier, static _ => 0);
        foreach (var package in packages)
        {
            if (!definedTierSet.Contains(package.Manifest.Tier))
            {
                return SkillOperationResult<SkillPackageCatalog>.FailureResult(
                    SkillFailureCodes.ManifestInvalid,
                    $"Generated SKILL package has an undefined tier: {package.Manifest.SkillName}/{package.Manifest.Tier.Value}");
            }

            counts[package.Manifest.Tier]++;
        }

        var availableTiers = definedTiers
            .Select(tier => new SkillTierPackageCount(tier, counts[tier]))
            .ToArray();
        var orderedPackages = packages
            .Where(package => selectedTierSet.Contains(package.Manifest.Tier))
            .OrderBy(static package => package.Manifest.SkillName, StringComparer.Ordinal)
            .ToArray();

        return SkillOperationResult<SkillPackageCatalog>.Success(new SkillPackageCatalog(selectedTiers, availableTiers, orderedPackages));
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

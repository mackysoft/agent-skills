using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Selection;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Distribution;

/// <summary> Provides selected SKILL packages from one validated generated bundle. </summary>
public sealed class SkillPackageProvider
{
    private readonly BundledSkillPackageRootResolver packageRootResolver;
    private readonly CanonicalSkillBundleReader bundleReader;
    private readonly CanonicalAgentDistributionBundleReader? agentDistributionBundleReader;
    private readonly SkillBundleDigestCalculator? skillBundleDigestCalculator;
    private readonly BundleSchemaVersionReader schemaVersionReader;

    /// <summary> Initializes a new instance of the <see cref="SkillPackageProvider" /> class. </summary>
    /// <param name="packageRootResolver"> The bundled generated SKILL package root resolver. </param>
    /// <param name="bundleReader"> The canonical bundle reader. </param>
    public SkillPackageProvider (
        BundledSkillPackageRootResolver packageRootResolver,
        CanonicalSkillBundleReader bundleReader)
    {
        this.packageRootResolver = packageRootResolver ?? throw new ArgumentNullException(nameof(packageRootResolver));
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
        schemaVersionReader = new BundleSchemaVersionReader();
    }

    /// <summary> Initializes a provider that reads both v1 SKILL bundles and the SKILL namespace of v2 mixed bundles. </summary>
    /// <param name="packageRootResolver"> The bundled generated package root resolver. </param>
    /// <param name="bundleReader"> The canonical v1 SKILL bundle reader. </param>
    /// <param name="agentDistributionBundleReader"> The canonical v2 mixed bundle reader. </param>
    /// <param name="skillBundleDigestCalculator"> The digest calculator used to project the v2 SKILL package set. </param>
    public SkillPackageProvider (
        BundledSkillPackageRootResolver packageRootResolver,
        CanonicalSkillBundleReader bundleReader,
        CanonicalAgentDistributionBundleReader agentDistributionBundleReader,
        SkillBundleDigestCalculator skillBundleDigestCalculator)
    {
        this.packageRootResolver = packageRootResolver ?? throw new ArgumentNullException(nameof(packageRootResolver));
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
        this.agentDistributionBundleReader = agentDistributionBundleReader ?? throw new ArgumentNullException(nameof(agentDistributionBundleReader));
        this.skillBundleDigestCalculator = skillBundleDigestCalculator ?? throw new ArgumentNullException(nameof(skillBundleDigestCalculator));
        schemaVersionReader = new BundleSchemaVersionReader();
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

    private async ValueTask<SkillOperationResult<SkillPackageBundle>> ReadBundleAsync (CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        AbsolutePath packageRoot;
        try
        {
            packageRoot = packageRootResolver.Resolve();
        }
        catch (DirectoryNotFoundException ex)
        {
            return SkillOperationResult<SkillPackageBundle>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                ex.Message);
        }

        var schemaVersionResult = await schemaVersionReader.ReadAsync(packageRoot, cancellationToken).ConfigureAwait(false);
        if (!schemaVersionResult.IsSuccess)
        {
            return SkillOperationResult<SkillPackageBundle>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                schemaVersionResult.Failure!.Message);
        }

        if (schemaVersionResult.Value == SkillBundleDefinition.CurrentSchemaVersion)
        {
            var bundleResult = await bundleReader.ReadAsync(packageRoot, cancellationToken).ConfigureAwait(false);
            return bundleResult.IsSuccess
                ? SkillOperationResult<SkillPackageBundle>.Success(
                    new SkillPackageBundle(bundleResult.Value!.Descriptor, bundleResult.Value.Packages))
                : SkillOperationResult<SkillPackageBundle>.FailureResult(
                    bundleResult.Failure!.Code,
                    bundleResult.Failure.Message);
        }

        if (schemaVersionResult.Value != AgentDistributionBundleDefinition.CurrentSchemaVersion
            || agentDistributionBundleReader is null
            || skillBundleDigestCalculator is null)
        {
            return SkillOperationResult<SkillPackageBundle>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"Generated bundle schema is not supported by this SKILL package provider: {schemaVersionResult.Value}");
        }

        var mixedBundleResult = await agentDistributionBundleReader.ReadAsync(packageRoot, cancellationToken).ConfigureAwait(false);
        if (!mixedBundleResult.IsSuccess)
        {
            return SkillOperationResult<SkillPackageBundle>.FailureResult(
                mixedBundleResult.Failure!.Code,
                mixedBundleResult.Failure.Message);
        }

        var mixedBundle = mixedBundleResult.Value!;
        if (mixedBundle.Skills.Count == 0)
        {
            return SkillOperationResult<SkillPackageBundle>.FailureResult(
                SkillFailureCodes.InputInvalid,
                "The v2 generated bundle does not contain any SKILL packages.");
        }

        var descriptor = new SkillBundleDescriptor(
            SkillBundleDefinition.CurrentSchemaVersion,
            mixedBundle.Descriptor.CatalogId,
            new SkillBundleVersion(mixedBundle.Descriptor.BundleVersion.Value),
            skillBundleDigestCalculator.ComputeDigest(mixedBundle.Skills));
        return SkillOperationResult<SkillPackageBundle>.Success(
            new SkillPackageBundle(descriptor, mixedBundle.Skills));
    }

    private static SkillOperationResult<SkillPackageCatalog> CreatePackageCatalog (
        SkillPackageBundle bundle,
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
        var resolvedPackages = SkillPackageDependencyResolver.Resolve(
            bundle.Packages,
            rootPackages.Select(static package => package.Manifest.SkillName).ToArray());

        return SkillOperationResult<SkillPackageCatalog>.Success(new SkillPackageCatalog(
            bundle.Descriptor,
            selectedCategories,
            selectedSkillNames,
            availableCategories,
            resolvedPackages));
    }

}

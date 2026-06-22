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

        var packagesResult = await GetPackagesAsync(cancellationToken).ConfigureAwait(false);
        if (!packagesResult.IsSuccess)
        {
            return packagesResult;
        }

        var definedTiers = definedTiersResult.Value!.ToHashSet();
        foreach (var package in packagesResult.Value!)
        {
            if (!definedTiers.Contains(package.Manifest.Tier))
            {
                return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                    SkillFailureCodes.ManifestInvalid,
                    $"Generated SKILL package has an undefined tier: {package.Manifest.SkillName}/{package.Manifest.Tier.Value}");
            }
        }

        var selectedTierSet = selectedTiers.ToHashSet();
        var packages = packagesResult.Value!
            .Where(package => selectedTierSet.Contains(package.Manifest.Tier))
            .OrderBy(static package => package.Manifest.SkillName, StringComparer.Ordinal)
            .ToArray();

        return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.Success(packages);
    }
}

using MackySoft.AgentSkills.Agents.Selection;
using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Distribution;

/// <summary> Provides selected custom-agent packages and their resolved SKILL dependencies from one v2 mixed bundle. </summary>
public sealed class AgentPackageProvider
{
    private readonly BundledAgentSkillsPackageRootResolver packageRootResolver;
    private readonly CanonicalAgentSkillsBundleReader bundleReader;

    /// <summary> Initializes an agent package provider. </summary>
    /// <param name="packageRootResolver"> The v2 mixed generated bundle root resolver. </param>
    /// <param name="bundleReader"> The mixed canonical bundle reader. </param>
    public AgentPackageProvider (
        BundledAgentSkillsPackageRootResolver packageRootResolver,
        CanonicalAgentSkillsBundleReader bundleReader)
    {
        this.packageRootResolver = packageRootResolver ?? throw new ArgumentNullException(nameof(packageRootResolver));
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
    }

    /// <summary> Gets every agent in the v2 bundle and the SKILL dependencies they require. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated through bundle reading. </param>
    /// <returns> The selected agent catalog, or a bundle-selection failure. </returns>
    public ValueTask<SkillOperationResult<AgentPackageCatalog>> GetPackageCatalogAsync (
        CancellationToken cancellationToken = default)
    {
        return GetPackageCatalogAsync([], [], cancellationToken);
    }

    /// <summary> Gets agents selected by exact category and agent-name literals and their resolved SKILL dependencies. </summary>
    /// <param name="selectedCategoryLiterals"> The selected agent categories. Empty selects every category present in the bundle. </param>
    /// <param name="selectedAgentNames"> The exact agent names. Empty selects every agent in the selected categories. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through bundle reading. </param>
    /// <returns> The selected agent catalog, or a bundle-selection failure. </returns>
    public async ValueTask<SkillOperationResult<AgentPackageCatalog>> GetPackageCatalogAsync (
        IReadOnlyList<string> selectedCategoryLiterals,
        IReadOnlyList<string> selectedAgentNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedCategoryLiterals);
        ArgumentNullException.ThrowIfNull(selectedAgentNames);
        cancellationToken.ThrowIfCancellationRequested();

        var agentNameSelectionResult = AgentNameLiteralParser.ParseOptionalAgentNames(selectedAgentNames);
        if (!agentNameSelectionResult.IsSuccess)
        {
            return SkillOperationResult<AgentPackageCatalog>.FailureResult(
                agentNameSelectionResult.Failure!.Code,
                agentNameSelectionResult.Failure.Message);
        }

        var bundleResult = await ReadBundleAsync(cancellationToken).ConfigureAwait(false);
        if (!bundleResult.IsSuccess)
        {
            return SkillOperationResult<AgentPackageCatalog>.FailureResult(
                bundleResult.Failure!.Code,
                bundleResult.Failure.Message);
        }

        return CreatePackageCatalog(
            bundleResult.Value!,
            selectedCategoryLiterals,
            agentNameSelectionResult.Value!);
    }

    private async ValueTask<SkillOperationResult<CanonicalAgentSkillsBundle>> ReadBundleAsync (CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        AbsolutePath packageRoot;
        try
        {
            packageRoot = packageRootResolver.Resolve();
        }
        catch (DirectoryNotFoundException exception)
        {
            return SkillOperationResult<CanonicalAgentSkillsBundle>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                exception.Message);
        }

        return await bundleReader.ReadAsync(packageRoot, cancellationToken).ConfigureAwait(false);
    }

    private static SkillOperationResult<AgentPackageCatalog> CreatePackageCatalog (
        CanonicalAgentSkillsBundle bundle,
        IReadOnlyList<string> selectedCategoryLiterals,
        IReadOnlyList<AgentName> selectedAgentNames)
    {
        var availableCategories = bundle.Agents
            .Select(static agent => agent.Manifest.Category)
            .Distinct()
            .OrderBy(static category => category.Value, StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<AgentCategory> selectedCategories;
        if (selectedCategoryLiterals.Count == 0)
        {
            selectedCategories = availableCategories;
        }
        else
        {
            var categorySelectionResult = AgentCategoryLiteralParser.ParseSelectedCategories(
                availableCategories,
                selectedCategoryLiterals);
            if (!categorySelectionResult.IsSuccess)
            {
                return SkillOperationResult<AgentPackageCatalog>.FailureResult(
                    categorySelectionResult.Failure!.Code,
                    categorySelectionResult.Failure.Message);
            }

            selectedCategories = categorySelectionResult.Value!;
        }

        var agentByName = bundle.Agents.ToDictionary(static agent => agent.Manifest.AgentName);
        var selectedCategorySet = selectedCategories.ToHashSet();
        foreach (var agentName in selectedAgentNames)
        {
            if (!agentByName.TryGetValue(agentName, out var agent))
            {
                return SkillOperationResult<AgentPackageCatalog>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"Selected agent name was not found: {agentName.Value}.");
            }

            if (!selectedCategorySet.Contains(agent.Manifest.Category))
            {
                return SkillOperationResult<AgentPackageCatalog>.FailureResult(
                    SkillFailureCodes.InputInvalid,
                    $"Selected agent name '{agentName.Value}' does not match selected categories: {string.Join(", ", selectedCategories.Select(static category => category.Value))}. Its category is: {agent.Manifest.Category.Value}.");
            }
        }

        var selectedAgentNameSet = selectedAgentNames.ToHashSet();
        var selectedAgents = bundle.Agents
            .Where(agent => selectedCategorySet.Contains(agent.Manifest.Category))
            .Where(agent => selectedAgentNameSet.Count == 0 || selectedAgentNameSet.Contains(agent.Manifest.AgentName))
            .OrderBy(static agent => agent.Manifest.AgentName.Value, StringComparer.Ordinal)
            .ToArray();
        var directSkillDependencies = selectedAgents
            .SelectMany(static agent => agent.Manifest.SkillDependencies)
            .Distinct()
            .OrderBy(static skillName => skillName.Value, StringComparer.Ordinal)
            .ToArray();
        var resolvedSkills = SkillPackageDependencyResolver.Resolve(bundle.Skills, directSkillDependencies);

        return SkillOperationResult<AgentPackageCatalog>.Success(new AgentPackageCatalog(
            bundle.Descriptor,
            selectedCategories,
            selectedAgentNames,
            selectedAgents,
            resolvedSkills));
    }
}

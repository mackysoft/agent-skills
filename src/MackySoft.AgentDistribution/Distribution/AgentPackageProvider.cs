using MackySoft.AgentDistribution.Agents.Selection;
using MackySoft.AgentDistribution.Bundles;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Distribution;

/// <summary> Provides selected custom-agent packages and their resolved SKILL dependencies from one v3 mixed bundle. </summary>
public sealed class AgentPackageProvider
{
    private readonly BundledAgentDistributionPackageRootResolver packageRootResolver;
    private readonly CanonicalAgentDistributionBundleReader bundleReader;

    /// <summary> Initializes an agent package provider. </summary>
    /// <param name="packageRootResolver"> The v3 mixed generated bundle root resolver. </param>
    /// <param name="bundleReader"> The mixed canonical bundle reader. </param>
    public AgentPackageProvider (
        BundledAgentDistributionPackageRootResolver packageRootResolver,
        CanonicalAgentDistributionBundleReader bundleReader)
    {
        this.packageRootResolver = packageRootResolver ?? throw new ArgumentNullException(nameof(packageRootResolver));
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
    }

    /// <summary> Gets every agent in the v3 bundle and the SKILL dependencies they require. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated through bundle reading. </param>
    /// <returns> The selected agent catalog, or a bundle-selection failure. </returns>
    public ValueTask<AgentDistributionOperationResult<AgentPackageCatalog>> GetPackageCatalogAsync (
        CancellationToken cancellationToken = default)
    {
        return GetPackageCatalogAsync([], cancellationToken);
    }

    /// <summary> Gets agents selected by exact agent-name literals and their resolved SKILL dependencies. </summary>
    /// <param name="selectedAgentNames"> The exact agent names. Empty selects every agent in the bundle. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through bundle reading. </param>
    /// <returns> The selected agent catalog, or a bundle-selection failure. </returns>
    public async ValueTask<AgentDistributionOperationResult<AgentPackageCatalog>> GetPackageCatalogAsync (
        IReadOnlyList<string> selectedAgentNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedAgentNames);
        cancellationToken.ThrowIfCancellationRequested();

        var agentNameSelectionResult = AgentNameLiteralParser.ParseOptionalAgentNames(selectedAgentNames);
        if (!agentNameSelectionResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentPackageCatalog>.FailureResult(
                agentNameSelectionResult.Failure!.Code,
                agentNameSelectionResult.Failure.Message);
        }

        var bundleResult = await ReadBundleAsync(cancellationToken).ConfigureAwait(false);
        if (!bundleResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AgentPackageCatalog>.FailureResult(
                bundleResult.Failure!.Code,
                bundleResult.Failure.Message);
        }

        return CreatePackageCatalog(bundleResult.Value!, agentNameSelectionResult.Value!);
    }

    private async ValueTask<AgentDistributionOperationResult<CanonicalAgentDistributionBundle>> ReadBundleAsync (CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        AbsolutePath packageRoot;
        try
        {
            packageRoot = packageRootResolver.Resolve();
        }
        catch (DirectoryNotFoundException exception)
        {
            return AgentDistributionOperationResult<CanonicalAgentDistributionBundle>.FailureResult(
                AgentDistributionFailureCodes.SourceInvalid,
                exception.Message);
        }

        return await bundleReader.ReadAsync(packageRoot, cancellationToken).ConfigureAwait(false);
    }

    private static AgentDistributionOperationResult<AgentPackageCatalog> CreatePackageCatalog (
        CanonicalAgentDistributionBundle bundle,
        IReadOnlyList<AgentName> selectedAgentNames)
    {
        var agentByName = bundle.Agents.ToDictionary(static agent => agent.Manifest.AgentName);
        foreach (var agentName in selectedAgentNames)
        {
            if (!agentByName.ContainsKey(agentName))
            {
                return AgentDistributionOperationResult<AgentPackageCatalog>.FailureResult(
                    AgentDistributionFailureCodes.InputInvalid,
                    $"Selected agent name was not found: {agentName.Value}.");
            }
        }

        var selectedAgentNameSet = selectedAgentNames.ToHashSet();
        var selectedAgents = bundle.Agents
            .Where(agent => selectedAgentNameSet.Count == 0 || selectedAgentNameSet.Contains(agent.Manifest.AgentName))
            .OrderBy(static agent => agent.Manifest.AgentName.Value, StringComparer.Ordinal)
            .ToArray();
        var directSkillDependencies = selectedAgents
            .SelectMany(static agent => agent.Manifest.SkillDependencies)
            .Distinct()
            .OrderBy(static skillName => skillName.Value, StringComparer.Ordinal)
            .ToArray();
        var resolvedSkills = SkillPackageDependencyResolver.Resolve(bundle.Skills, directSkillDependencies);

        return AgentDistributionOperationResult<AgentPackageCatalog>.Success(new AgentPackageCatalog(
            bundle.Descriptor,
            selectedAgentNames,
            selectedAgents,
            resolvedSkills));
    }
}

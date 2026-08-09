using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Distribution;

namespace MackySoft.AgentDistribution.Agents.Installation.Requests;

/// <summary> Represents one custom-agent prune request. </summary>
public sealed class AgentPruneInput
{
    /// <summary> Initializes one immutable prune input. </summary>
    /// <param name="currentCatalog"> The complete unfiltered current catalog. </param>
    /// <param name="agentTargetRequest"> The custom-agent artifact target. </param>
    /// <param name="dryRun"> Whether to plan without deletions. </param>
    /// <param name="force"> Whether locally modified orphan artifacts may be deleted. </param>
    public AgentPruneInput (
        AgentPackageCatalog currentCatalog,
        AgentTargetRequest agentTargetRequest,
        bool dryRun = false,
        bool force = false,
        IReadOnlyList<AgentCategory>? selectedCategories = null,
        IReadOnlyList<AgentName>? selectedAgentNames = null)
    {
        CurrentCatalog = currentCatalog ?? throw new ArgumentNullException(nameof(currentCatalog));
        if (currentCatalog.SelectedAgentNames.Count != 0)
        {
            throw new ArgumentException("Prune requires the complete current agent catalog without selection filters.", nameof(currentCatalog));
        }

        AgentTargetRequest = agentTargetRequest ?? throw new ArgumentNullException(nameof(agentTargetRequest));
        DryRun = dryRun;
        Force = force;
        SelectedCategories = CopyOptional(selectedCategories, nameof(selectedCategories));
        SelectedAgentNames = CopyOptional(selectedAgentNames, nameof(selectedAgentNames));
    }

    /// <summary> Gets the complete current catalog used to identify removed agents. </summary>
    public AgentPackageCatalog CurrentCatalog { get; }

    /// <summary> Gets the custom-agent artifact target. </summary>
    public AgentTargetRequest AgentTargetRequest { get; }

    /// <summary> Gets whether to plan without deletions. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether locally modified orphan artifacts may be deleted. </summary>
    public bool Force { get; }

    /// <summary> Gets optional installed-agent category filters. Empty means every category. </summary>
    public IReadOnlyList<AgentCategory> SelectedCategories { get; }

    /// <summary> Gets optional exact installed-agent name filters, including names absent from the current catalog. Empty means every name. </summary>
    public IReadOnlyList<AgentName> SelectedAgentNames { get; }

    private static IReadOnlyList<T> CopyOptional<T> (IReadOnlyList<T>? values, string parameterName)
        where T : class
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        var snapshot = values.ToArray();
        if (snapshot.Any(static value => value is null))
        {
            throw new ArgumentException("Prune filters must not contain null items.", parameterName);
        }

        return Array.AsReadOnly(snapshot.Distinct().ToArray());
    }
}

using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Agents.Selection;

/// <summary> Parses optional exact custom-agent name selections. </summary>
public static class AgentNameLiteralParser
{
    /// <summary> Parses selected agent name literals. </summary>
    /// <param name="selectedAgentNames"> The exact agent name literals selected by the caller. </param>
    /// <returns> A deduplicated immutable agent-name selection, or an input failure. </returns>
    public static AgentDistributionOperationResult<IReadOnlyList<AgentName>> ParseOptionalAgentNames (
        IReadOnlyList<string> selectedAgentNames)
    {
        ArgumentNullException.ThrowIfNull(selectedAgentNames);

        var normalizedAgentNames = new List<AgentName>(selectedAgentNames.Count);
        var selectedAgentNameSet = new HashSet<AgentName>();
        foreach (var agentNameLiteral in selectedAgentNames)
        {
            if (!AgentName.TryCreate(agentNameLiteral, out var agentName))
            {
                return AgentDistributionOperationResult<IReadOnlyList<AgentName>>.FailureResult(
                    AgentDistributionFailureCodes.InputInvalid,
                    $"Agent name literal is invalid: {agentNameLiteral ?? "<null>"}.");
            }

            if (selectedAgentNameSet.Add(agentName!))
            {
                normalizedAgentNames.Add(agentName);
            }
        }

        return AgentDistributionOperationResult<IReadOnlyList<AgentName>>.Success(
            Array.AsReadOnly(normalizedAgentNames.ToArray()));
    }
}

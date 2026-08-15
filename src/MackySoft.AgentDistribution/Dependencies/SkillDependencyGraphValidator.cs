using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Dependencies;

internal static class SkillDependencyGraphValidator
{
    /// <summary>
    /// Validates the structural invariants of a closed directed SKILL dependency graph.
    /// </summary>
    /// <remarks>
    /// Each dictionary key is a graph node and its value is that node's outgoing edges. This validator
    /// does not resolve or select dependencies; callers provide the complete graph and choose the
    /// boundary-specific failure code and label.
    /// </remarks>
    /// <param name="dependenciesBySkillName"> The complete graph as skill names and their declared dependencies. </param>
    /// <param name="failureCode"> The failure code owned by the calling boundary. </param>
    /// <param name="graphLabel"> The boundary-specific label used in deterministic failure messages. </param>
    /// <returns> A success result when every edge targets a defined distinct node and the graph is acyclic. </returns>
    public static AgentDistributionOperationResult<bool> ValidateClosedGraph (
        IReadOnlyDictionary<SkillName, IReadOnlyList<SkillName>> dependenciesBySkillName,
        AgentDistributionFailureCode failureCode,
        string graphLabel)
    {
        ArgumentNullException.ThrowIfNull(dependenciesBySkillName);
        ArgumentException.ThrowIfNullOrWhiteSpace(graphLabel);

        foreach (var (skillName, dependencies) in dependenciesBySkillName.OrderBy(static item => item.Key.Value, StringComparer.Ordinal))
        {
            ArgumentNullException.ThrowIfNull(dependencies);
            foreach (var dependency in dependencies.OrderBy(static dependency => dependency.Value, StringComparer.Ordinal))
            {
                if (skillName == dependency)
                {
                    return AgentDistributionOperationResult<bool>.FailureResult(failureCode, $"{graphLabel} dependency must not reference itself: {skillName.Value}.");
                }

                if (!dependenciesBySkillName.ContainsKey(dependency))
                {
                    return AgentDistributionOperationResult<bool>.FailureResult(failureCode, $"{graphLabel} dependency was not found: {skillName.Value} -> {dependency.Value}.");
                }
            }
        }

        var states = dependenciesBySkillName.Keys.ToDictionary(static name => name, static _ => VisitState.NotVisited);
        var stack = new List<SkillName>();
        foreach (var skillName in dependenciesBySkillName.Keys.OrderBy(static skillName => skillName.Value, StringComparer.Ordinal))
        {
            var result = Visit(skillName, dependenciesBySkillName, states, stack, failureCode, graphLabel);
            if (!result.IsSuccess)
            {
                return result;
            }
        }

        return AgentDistributionOperationResult<bool>.Success(true);
    }

    private static AgentDistributionOperationResult<bool> Visit (
        SkillName skillName,
        IReadOnlyDictionary<SkillName, IReadOnlyList<SkillName>> dependenciesBySkillName,
        Dictionary<SkillName, VisitState> states,
        List<SkillName> stack,
        AgentDistributionFailureCode failureCode,
        string graphLabel)
    {
        var state = states[skillName];
        if (state == VisitState.Visited)
        {
            return AgentDistributionOperationResult<bool>.Success(true);
        }

        if (state == VisitState.Visiting)
        {
            var cycleStart = stack.IndexOf(skillName);
            var cycle = stack
                .Skip(cycleStart)
                .Concat([skillName])
                .Select(static skillName => skillName.Value)
                .ToArray();
            return AgentDistributionOperationResult<bool>.FailureResult(failureCode, $"{graphLabel} dependency cycle was found: {string.Join(" -> ", cycle)}.");
        }

        states[skillName] = VisitState.Visiting;
        stack.Add(skillName);
        foreach (var dependency in dependenciesBySkillName[skillName].OrderBy(static dependency => dependency.Value, StringComparer.Ordinal))
        {
            var result = Visit(dependency, dependenciesBySkillName, states, stack, failureCode, graphLabel);
            if (!result.IsSuccess)
            {
                return result;
            }
        }

        stack.RemoveAt(stack.Count - 1);
        states[skillName] = VisitState.Visited;
        return AgentDistributionOperationResult<bool>.Success(true);
    }

    private enum VisitState
    {
        NotVisited,
        Visiting,
        Visited,
    }
}

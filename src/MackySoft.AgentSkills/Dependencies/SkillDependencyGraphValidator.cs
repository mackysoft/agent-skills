using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Dependencies;

internal static class SkillDependencyGraphValidator
{
    public static SkillOperationResult<bool> Validate (
        IReadOnlyDictionary<SkillName, IReadOnlyList<SkillName>> dependenciesBySkillName,
        SkillFailureCode failureCode,
        string graphLabel)
    {
        ArgumentNullException.ThrowIfNull(dependenciesBySkillName);
        ArgumentException.ThrowIfNullOrWhiteSpace(graphLabel);

        foreach (var (skillName, dependencies) in dependenciesBySkillName.OrderBy(static item => item.Key.Value, StringComparer.Ordinal))
        {
            ArgumentNullException.ThrowIfNull(dependencies);
            foreach (var dependency in dependencies.OrderBy(static dependency => dependency.Value, StringComparer.Ordinal))
            {
                if (string.Equals(skillName.Value, dependency.Value, StringComparison.Ordinal))
                {
                    return SkillOperationResult<bool>.FailureResult(failureCode, $"{graphLabel} dependency must not reference itself: {skillName.Value}.");
                }

                if (!dependenciesBySkillName.ContainsKey(dependency))
                {
                    return SkillOperationResult<bool>.FailureResult(failureCode, $"{graphLabel} dependency was not found: {skillName.Value} -> {dependency.Value}.");
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

        return SkillOperationResult<bool>.Success(true);
    }

    private static SkillOperationResult<bool> Visit (
        SkillName skillName,
        IReadOnlyDictionary<SkillName, IReadOnlyList<SkillName>> dependenciesBySkillName,
        Dictionary<SkillName, VisitState> states,
        List<SkillName> stack,
        SkillFailureCode failureCode,
        string graphLabel)
    {
        var state = states[skillName];
        if (state == VisitState.Visited)
        {
            return SkillOperationResult<bool>.Success(true);
        }

        if (state == VisitState.Visiting)
        {
            var cycleStart = stack.IndexOf(skillName);
            var cycle = stack
                .Skip(cycleStart)
                .Concat([skillName])
                .Select(static skillName => skillName.Value)
                .ToArray();
            return SkillOperationResult<bool>.FailureResult(failureCode, $"{graphLabel} dependency cycle was found: {string.Join(" -> ", cycle)}.");
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
        return SkillOperationResult<bool>.Success(true);
    }

    private enum VisitState
    {
        NotVisited,
        Visiting,
        Visited,
    }
}

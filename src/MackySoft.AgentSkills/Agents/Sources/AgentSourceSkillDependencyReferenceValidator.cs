using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Sources;

namespace MackySoft.AgentSkills.Agents.Sources;

/// <summary> Verifies that agent skill declarations and source references are identical. </summary>
internal static class AgentSourceSkillDependencyReferenceValidator
{
    /// <summary> Validates agent references against the known skill set. </summary>
    public static SkillOperationResult<bool> Validate (IReadOnlyList<AgentSourceDefinition> agents, IReadOnlyList<SkillSourceDefinition> skills)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(skills);
        var knownSkills = skills.Select(static skill => skill.Metadata.SkillName).ToHashSet();
        foreach (var agent in agents.OrderBy(static agent => agent.Metadata.AgentName.Value, StringComparer.Ordinal))
        {
            var declared = agent.Metadata.SkillDependencies.ToHashSet();
            var missingSkills = declared.Where(skill => !knownSkills.Contains(skill)).Select(static skill => skill.Value).Order(StringComparer.Ordinal).ToArray();
            if (missingSkills.Length != 0)
            {
                return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.SourceInvalid, $"agent.json references missing skills for '{agent.Metadata.AgentName.Value}': {string.Join(", ", missingSkills)}.");
            }

            var referenced = SkillDependencyReferenceScanner.FindReferences([agent.InstructionsTemplate]).Where(knownSkills.Contains).ToHashSet();
            var missingReferences = declared.Except(referenced).Select(static skill => skill.Value).Order(StringComparer.Ordinal).ToArray();
            if (missingReferences.Length != 0)
            {
                return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.SourceInvalid, $"agent.json skillDependencies are not referenced in source text for '{agent.Metadata.AgentName.Value}': {string.Join(", ", missingReferences)}.");
            }

            var missingDeclarations = referenced.Except(declared).Select(static skill => skill.Value).Order(StringComparer.Ordinal).ToArray();
            if (missingDeclarations.Length != 0)
            {
                return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.SourceInvalid, $"Agent source text references undeclared skill dependencies for '{agent.Metadata.AgentName.Value}': {string.Join(", ", missingDeclarations)}.");
            }
        }

        return SkillOperationResult<bool>.Success(true);
    }
}

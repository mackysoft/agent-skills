using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Installation.State;

/// <summary> Resolves the host-unobserved ownership-state file for one agent. </summary>
public sealed class AgentInstallationStatePathResolver
{
    /// <summary> Resolves one canonical ownership-state file path. </summary>
    public SkillOperationResult<string> Resolve (AgentResolvedTarget target, SkillCatalogId catalogId, AgentName agentName)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(catalogId);
        ArgumentNullException.ThrowIfNull(agentName);
        var catalogDirectory = AgentPathGuard.ResolveUnderRoot(target.StateRoot, Path.Combine(target.StateRoot, catalogId.Value));
        if (!catalogDirectory.IsSuccess)
        {
            return SkillOperationResult<string>.FailureResult(catalogDirectory.Failure!.Code, catalogDirectory.Failure.Message);
        }

        return AgentPathGuard.ResolveUnderRoot(catalogDirectory.Value!, Path.Combine(catalogDirectory.Value!, $"{agentName.Value}.json"));
    }
}

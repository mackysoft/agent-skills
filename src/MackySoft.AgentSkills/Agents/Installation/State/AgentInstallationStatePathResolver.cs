using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Installation.State;

/// <summary> Resolves the host-unobserved ownership-state file for one agent. </summary>
public sealed class AgentInstallationStatePathResolver
{
    /// <summary> Resolves one canonical ownership-state file path. </summary>
    public SkillOperationResult<AbsolutePath> Resolve (AgentResolvedTarget target, SkillCatalogId catalogId, AgentName agentName)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(catalogId);
        ArgumentNullException.ThrowIfNull(agentName);
        var relativePath = RootRelativePath.Parse($"{catalogId.Value}/{agentName.Value}.json");
        return AgentPathGuard.Validate(ContainedPath.Create(target.StateRoot, relativePath));
    }
}

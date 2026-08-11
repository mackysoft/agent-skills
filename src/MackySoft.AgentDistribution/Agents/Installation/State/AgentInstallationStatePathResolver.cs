using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Catalogs;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Installation.State;

/// <summary> Resolves the host-unobserved ownership-state file for one agent. </summary>
public sealed class AgentInstallationStatePathResolver
{
    /// <summary> Resolves one canonical ownership-state file path. </summary>
    public AgentDistributionOperationResult<AbsolutePath> Resolve (AgentResolvedTarget target, AgentDistributionCatalogId catalogId, AgentName agentName)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(catalogId);
        ArgumentNullException.ThrowIfNull(agentName);
        var relativePath = RootRelativePath.Parse($"{catalogId.Value}/{agentName.Value}.json");
        return AgentPathGuard.Validate(ContainedPath.Create(target.StateRoot, relativePath));
    }
}

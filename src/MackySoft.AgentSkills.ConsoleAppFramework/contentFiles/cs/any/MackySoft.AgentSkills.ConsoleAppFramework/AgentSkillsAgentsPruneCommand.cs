#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Reporting;

namespace MackySoft.AgentSkills.ConsoleAppFramework;

internal sealed class AgentSkillsAgentsPruneCommand
{
    private readonly AgentSkillsAgentsCommandRunner runner;
    private readonly IAgentSkillsCommandResultEmitter emitter;

    public AgentSkillsAgentsPruneCommand (AgentSkillsAgentsCommandRunner runner, IAgentSkillsCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Prunes custom agents removed from the current catalog without removing SKILL dependencies. </summary>
    /// <param name="repositoryRoot">Project root.</param>
    /// <param name="agentTargetDir">Exact custom-agent artifact target directory.</param>
    [Command("prune")]
    public async Task<int> PruneAsync (
        string? host = null,
        string[]? category = null,
        string[]? agent = null,
        string? scope = null,
        string? repositoryRoot = null,
        string? agentTargetDir = null,
        bool dryRun = false,
        bool force = false,
        bool pretty = false,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.PruneAsync(new AgentSkillsAgentPruneCommandRequest(host, category, agent, scope, repositoryRoot, agentTargetDir, dryRun, force), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentSkillsCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

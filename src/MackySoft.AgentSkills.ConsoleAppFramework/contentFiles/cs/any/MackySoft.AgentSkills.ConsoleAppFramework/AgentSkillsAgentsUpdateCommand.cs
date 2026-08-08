#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Reporting;

namespace MackySoft.AgentSkills.ConsoleAppFramework;

internal sealed class AgentSkillsAgentsUpdateCommand
{
    private readonly AgentSkillsAgentsCommandRunner runner;
    private readonly IAgentSkillsCommandResultEmitter emitter;

    public AgentSkillsAgentsUpdateCommand (AgentSkillsAgentsCommandRunner runner, IAgentSkillsCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Updates selected custom agents and their resolved SKILL dependencies. </summary>
    /// <param name="repositoryRoot">Project root.</param>
    /// <param name="agentTargetDir">Exact custom-agent artifact target directory.</param>
    /// <param name="skillTargetDir">Exact resolved-SKILL target directory.</param>
    [Command("update")]
    public async Task<int> UpdateAsync (
        string? host = null,
        string[]? category = null,
        string[]? agent = null,
        string? scope = null,
        string? repositoryRoot = null,
        string? agentTargetDir = null,
        string? skillTargetDir = null,
        bool dryRun = false,
        bool force = false,
        bool printDiff = false,
        bool pretty = false,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.UpdateAsync(new AgentSkillsAgentUpdateCommandRequest(host, category, agent, scope, repositoryRoot, agentTargetDir, skillTargetDir, dryRun, force, printDiff), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentSkillsCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Reporting;

namespace MackySoft.AgentSkills.ConsoleAppFramework;

internal sealed class AgentSkillsAgentsListCommand
{
    private readonly AgentSkillsAgentsCommandRunner runner;
    private readonly IAgentSkillsCommandResultEmitter emitter;

    public AgentSkillsAgentsListCommand (AgentSkillsAgentsCommandRunner runner, IAgentSkillsCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Lists custom agents and their resolved SKILL dependencies. </summary>
    [Command("list")]
    public async Task<int> ListAsync (
        string[]? category = null,
        string[]? agent = null,
        bool pretty = false,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.ListAsync(new AgentSkillsAgentListCommandRequest(category, agent), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentSkillsCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

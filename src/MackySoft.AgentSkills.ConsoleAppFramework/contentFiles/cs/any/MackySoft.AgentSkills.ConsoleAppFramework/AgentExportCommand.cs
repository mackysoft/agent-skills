#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Reporting;

namespace MackySoft.AgentSkills.ConsoleAppFramework;

internal sealed class AgentExportCommand
{
    private readonly AgentCommandRunner runner;
    private readonly IAgentSkillsCommandResultEmitter emitter;

    public AgentExportCommand (AgentCommandRunner runner, IAgentSkillsCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Exports selected custom agents and their resolved SKILL dependencies. </summary>
    [Command("export")]
    public async Task<int> ExportAsync (
        string? host = null,
        string[]? category = null,
        string[]? agent = null,
        string? output = null,
        string? format = null,
        bool pretty = false,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.ExportAsync(new AgentExportCommandRequest(host, category, agent, output, format), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentSkillsCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Reporting;

namespace MackySoft.AgentSkills.ConsoleAppFramework;

internal sealed class AgentSkillsExportCommand
{
    private readonly AgentSkillsCommandRunner runner;
    private readonly IAgentSkillsCommandResultEmitter emitter;

    public AgentSkillsExportCommand (
        AgentSkillsCommandRunner runner,
        IAgentSkillsCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    [Command("export")]
    public async Task<int> ExportAsync (
        string? host = null,
        string[]? tier = null,
        string[]? skill = null,
        string? output = null,
        string? format = null,
        bool pretty = false,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.ExportAsync(new AgentSkillsExportCommandRequest(host, tier, skill, output, format), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentSkillsCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

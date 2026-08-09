#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentDistribution.Hosting.Commands;
using MackySoft.AgentDistribution.Hosting.Reporting;

namespace MackySoft.AgentDistribution.ConsoleAppFramework;

internal sealed class SkillExportCommand
{
    private readonly SkillCommandRunner runner;
    private readonly IAgentDistributionCommandResultEmitter emitter;

    public SkillExportCommand (
        SkillCommandRunner runner,
        IAgentDistributionCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Exports skills. </summary>
    [Command("export")]
    public async Task<int> ExportAsync (
        string? host = null,
        string[]? category = null,
        string[]? skill = null,
        string? output = null,
        string? format = null,
        bool pretty = false,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.ExportAsync(new SkillExportCommandRequest(host, category, skill, output, format), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentDistributionCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

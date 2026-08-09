#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentDistribution.Hosting.Commands;
using MackySoft.AgentDistribution.Hosting.Reporting;

namespace MackySoft.AgentDistribution.ConsoleAppFramework;

internal sealed class SkillListCommand
{
    private readonly SkillCommandRunner runner;
    private readonly IAgentDistributionCommandResultEmitter emitter;

    public SkillListCommand (
        SkillCommandRunner runner,
        IAgentDistributionCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Lists skills. </summary>
    [Command("list")]
    public async Task<int> ListAsync (
        string[]? category = null,
        string[]? skill = null,
        bool pretty = false,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.ListAsync(new SkillListCommandRequest(category, skill), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentDistributionCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

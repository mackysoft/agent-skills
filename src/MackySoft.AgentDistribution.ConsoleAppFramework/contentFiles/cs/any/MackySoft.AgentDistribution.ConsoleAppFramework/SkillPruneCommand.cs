#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentDistribution.Hosting.Commands;
using MackySoft.AgentDistribution.Hosting.Reporting;

namespace MackySoft.AgentDistribution.ConsoleAppFramework;

internal sealed class SkillPruneCommand
{
    private readonly SkillCommandRunner runner;
    private readonly IAgentDistributionCommandResultEmitter emitter;

    public SkillPruneCommand (
        SkillCommandRunner runner,
        IAgentDistributionCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Prunes removed skills. </summary>
    /// <param name="repositoryRoot">Project root.</param>
    /// <param name="targetDir">Host target directory override.</param>
    /// <param name="dryRun">Report planned changes without writing files.</param>
    [Command("prune")]
    public async Task<int> PruneAsync (
        string? host = null,
        string[]? category = null,
        string[]? skill = null,
        string? scope = null,
        string? repositoryRoot = null,
        string? targetDir = null,
        bool dryRun = false,
        bool force = false,
        bool pretty = false,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.PruneAsync(new SkillPruneCommandRequest(host, category, skill, scope, repositoryRoot, targetDir, dryRun, force), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentDistributionCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentDistribution.Hosting.Commands;
using MackySoft.AgentDistribution.Hosting.Reporting;

namespace MackySoft.AgentDistribution.ConsoleAppFramework;

internal sealed class SkillInstallCommand
{
    private readonly SkillCommandRunner runner;
    private readonly IAgentDistributionCommandResultEmitter emitter;

    public SkillInstallCommand (
        SkillCommandRunner runner,
        IAgentDistributionCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Installs skills. </summary>
    /// <param name="repositoryRoot">Project root.</param>
    /// <param name="targetDir">Host target directory override.</param>
    /// <param name="dryRun">Report planned changes without writing files.</param>
    /// <param name="printDiff">Include file diffs in the operation report.</param>
    [Command("install")]
    public async Task<int> InstallAsync (
        string? host = null,
        string[]? category = null,
        string[]? skill = null,
        string? scope = null,
        string? repositoryRoot = null,
        string? targetDir = null,
        bool dryRun = false,
        bool force = false,
        bool printDiff = false,
        bool pretty = false,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.InstallAsync(new SkillInstallCommandRequest(host, category, skill, scope, repositoryRoot, targetDir, dryRun, force, printDiff), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentDistributionCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentDistribution.Hosting.Commands;
using MackySoft.AgentDistribution.Hosting.Reporting;

namespace MackySoft.AgentDistribution.ConsoleAppFramework;

internal sealed class AgentInstallCommand
{
    private readonly AgentCommandRunner runner;
    private readonly IAgentDistributionCommandResultEmitter emitter;

    public AgentInstallCommand (AgentCommandRunner runner, IAgentDistributionCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Installs selected custom agents and their resolved SKILL dependencies. </summary>
    /// <param name="repositoryRoot">Project root.</param>
    /// <param name="agentTargetDir">Exact custom-agent artifact target directory.</param>
    /// <param name="skillTargetDir">Exact resolved-SKILL target directory.</param>
    [Command("install")]
    public async Task<int> InstallAsync (
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
        var result = await runner.InstallAsync(new AgentInstallCommandRequest(host, category, agent, scope, repositoryRoot, agentTargetDir, skillTargetDir, dryRun, force, printDiff), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentDistributionCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

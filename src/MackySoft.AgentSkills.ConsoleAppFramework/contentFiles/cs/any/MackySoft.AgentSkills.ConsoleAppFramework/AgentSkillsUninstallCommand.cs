#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Reporting;

namespace MackySoft.AgentSkills.ConsoleAppFramework;

internal sealed class AgentSkillsUninstallCommand
{
    private readonly AgentSkillsCommandRunner runner;
    private readonly IAgentSkillsCommandResultEmitter emitter;

    public AgentSkillsUninstallCommand(
        AgentSkillsCommandRunner runner,
        IAgentSkillsCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Uninstalls Agent Skills. </summary>
    /// <param name="repositoryRoot">Project root.</param>
    /// <param name="targetDir">Host target directory override.</param>
    /// <param name="dryRun">Report planned changes without writing files.</param>
    [Command("uninstall")]
    public async Task<int> UninstallAsync(
        string? host = null,
        string[]? tier = null,
        string[]? skill = null,
        string? scope = null,
        string? repositoryRoot = null,
        string? targetDir = null,
        bool dryRun = false,
        bool force = false,
        bool pretty = false,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.UninstallAsync(new AgentSkillsUninstallCommandRequest(host, tier, skill, scope, repositoryRoot, targetDir, dryRun, force), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentSkillsCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

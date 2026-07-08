#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Reporting;

namespace MackySoft.AgentSkills.ConsoleAppFramework;

internal sealed class AgentSkillsPruneCommand
{
    private readonly AgentSkillsCommandRunner runner;
    private readonly IAgentSkillsCommandResultEmitter emitter;

    public AgentSkillsPruneCommand (
        AgentSkillsCommandRunner runner,
        IAgentSkillsCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Prunes removed Agent Skills. </summary>
    /// <param name="repositoryRoot">--repository-root, Project root.</param>
    /// <param name="targetDir">--target-dir, Host target directory override.</param>
    /// <param name="dryRun">--dry-run, Report planned changes without writing files.</param>
    [Command("prune")]
    public async Task<int> PruneAsync (
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
        var result = await runner.PruneAsync(new AgentSkillsPruneCommandRequest(host, tier, skill, scope, repositoryRoot, targetDir, dryRun, force), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentSkillsCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

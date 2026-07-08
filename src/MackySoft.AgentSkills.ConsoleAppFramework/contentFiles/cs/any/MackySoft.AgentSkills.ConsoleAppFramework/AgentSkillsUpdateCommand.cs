#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Reporting;

namespace MackySoft.AgentSkills.ConsoleAppFramework;

internal sealed class AgentSkillsUpdateCommand
{
    private readonly AgentSkillsCommandRunner runner;
    private readonly IAgentSkillsCommandResultEmitter emitter;

    public AgentSkillsUpdateCommand (
        AgentSkillsCommandRunner runner,
        IAgentSkillsCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Updates Agent Skills. </summary>
    /// <param name="repositoryRoot">--repository-root, Project root.</param>
    /// <param name="targetDir">--target-dir, Host target directory override.</param>
    /// <param name="dryRun">--dry-run, Report planned changes without writing files.</param>
    /// <param name="printDiff">--print-diff, Include file diffs in the operation report.</param>
    [Command("update")]
    public async Task<int> UpdateAsync (
        string? host = null,
        string[]? tier = null,
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
        var result = await runner.UpdateAsync(new AgentSkillsUpdateCommandRequest(host, tier, skill, scope, repositoryRoot, targetDir, dryRun, force, printDiff), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentSkillsCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

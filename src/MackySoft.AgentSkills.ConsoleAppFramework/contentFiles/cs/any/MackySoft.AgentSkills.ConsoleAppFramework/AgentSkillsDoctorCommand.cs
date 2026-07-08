#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Reporting;

namespace MackySoft.AgentSkills.ConsoleAppFramework;

internal sealed class AgentSkillsDoctorCommand
{
    private readonly AgentSkillsCommandRunner runner;
    private readonly IAgentSkillsCommandResultEmitter emitter;

    public AgentSkillsDoctorCommand (
        AgentSkillsCommandRunner runner,
        IAgentSkillsCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Diagnoses installed Agent Skills. </summary>
    /// <param name="repositoryRoot">--repository-root, Project root.</param>
    /// <param name="targetDir">--target-dir, Host target directory override.</param>
    [Command("doctor")]
    public async Task<int> DoctorAsync (
        string? host = null,
        string[]? tier = null,
        string[]? skill = null,
        string? scope = null,
        string? repositoryRoot = null,
        string? targetDir = null,
        bool pretty = false,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.DoctorAsync(new AgentSkillsDoctorCommandRequest(host, tier, skill, scope, repositoryRoot, targetDir), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentSkillsCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

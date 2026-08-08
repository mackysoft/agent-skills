#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Reporting;

namespace MackySoft.AgentSkills.ConsoleAppFramework;

internal sealed class AgentSkillsAgentsDoctorCommand
{
    private readonly AgentSkillsAgentsCommandRunner runner;
    private readonly IAgentSkillsCommandResultEmitter emitter;

    public AgentSkillsAgentsDoctorCommand (AgentSkillsAgentsCommandRunner runner, IAgentSkillsCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Diagnoses selected custom agents and their resolved SKILL dependencies without writing files. </summary>
    /// <param name="repositoryRoot">Project root.</param>
    /// <param name="agentTargetDir">Exact custom-agent artifact target directory.</param>
    /// <param name="skillTargetDir">Exact resolved-SKILL target directory.</param>
    [Command("doctor")]
    public async Task<int> DoctorAsync (
        string? host = null,
        string[]? category = null,
        string[]? agent = null,
        string? scope = null,
        string? repositoryRoot = null,
        string? agentTargetDir = null,
        string? skillTargetDir = null,
        bool pretty = false,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.DoctorAsync(new AgentSkillsAgentDoctorCommandRequest(host, category, agent, scope, repositoryRoot, agentTargetDir, skillTargetDir), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentSkillsCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

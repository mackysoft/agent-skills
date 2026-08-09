#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleAppFramework;
using MackySoft.AgentDistribution.Hosting.Commands;
using MackySoft.AgentDistribution.Hosting.Reporting;

namespace MackySoft.AgentDistribution.ConsoleAppFramework;

internal sealed class SkillDoctorCommand
{
    private readonly SkillCommandRunner runner;
    private readonly IAgentDistributionCommandResultEmitter emitter;

    public SkillDoctorCommand (
        SkillCommandRunner runner,
        IAgentDistributionCommandResultEmitter emitter)
    {
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary> Diagnoses installed skills. </summary>
    /// <param name="repositoryRoot">Project root.</param>
    /// <param name="targetDir">Host target directory override.</param>
    [Command("doctor")]
    public async Task<int> DoctorAsync (
        string? host = null,
        string[]? category = null,
        string[]? skill = null,
        string? scope = null,
        string? repositoryRoot = null,
        string? targetDir = null,
        bool pretty = false,
        CancellationToken cancellationToken = default)
    {
        var result = await runner.DoctorAsync(new SkillDoctorCommandRequest(host, category, skill, scope, repositoryRoot, targetDir), cancellationToken).ConfigureAwait(false);
        return await emitter.EmitAsync(result, new AgentDistributionCommandOutputOptions(pretty), cancellationToken).ConfigureAwait(false);
    }
}

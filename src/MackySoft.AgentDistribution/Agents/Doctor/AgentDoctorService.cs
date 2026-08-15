using MackySoft.AgentDistribution.Agents.Installation.State;
using MackySoft.AgentDistribution.Agents.Installation.Targeting;
using MackySoft.AgentDistribution.Doctor;
using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Agents.Doctor;

/// <summary> Diagnoses selected custom-agent packages, host artifacts, installed state, and resolved SKILL dependencies. </summary>
public sealed class AgentDoctorService
{
    private static readonly AgentDistributionFailureCode HealthyCode = new("AGENT_DOCTOR_OK");
    private readonly AgentInstallTargetResolver agentTargetResolver;
    private readonly SkillInstallTargetResolver skillTargetResolver;
    private readonly AgentInstalledTargetInspector targetInspector;
    private readonly SkillDoctorService skillDoctorService;

    /// <summary> Initializes a custom-agent doctor service. </summary>
    public AgentDoctorService (
        AgentInstallTargetResolver agentTargetResolver,
        SkillInstallTargetResolver skillTargetResolver,
        AgentInstalledTargetInspector targetInspector,
        SkillDoctorService skillDoctorService)
    {
        this.agentTargetResolver = agentTargetResolver ?? throw new ArgumentNullException(nameof(agentTargetResolver));
        this.skillTargetResolver = skillTargetResolver ?? throw new ArgumentNullException(nameof(skillTargetResolver));
        this.targetInspector = targetInspector ?? throw new ArgumentNullException(nameof(targetInspector));
        this.skillDoctorService = skillDoctorService ?? throw new ArgumentNullException(nameof(skillDoctorService));
    }

    /// <summary> Diagnoses all selected agents and their resolved SKILL dependency closure without writing files. </summary>
    public async ValueTask<AgentDistributionOperationResult<AgentDoctorResult>> DiagnoseAsync (
        AgentDoctorInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        var agentTargetResult = agentTargetResolver.ResolveTarget(input.AgentTargetRequest);
        if (!agentTargetResult.IsSuccess)
        {
            return Failure(agentTargetResult.Failure!);
        }

        var skillTargetResult = skillTargetResolver.ResolveTarget(
            input.SkillTargetRequest,
            input.Catalog.CatalogId);
        if (!skillTargetResult.IsSuccess)
        {
            return Failure(skillTargetResult.Failure!);
        }

        var agentTarget = agentTargetResult.Value!;
        var diagnostics = new List<AgentDoctorDiagnostic>();
        foreach (var package in input.Catalog.SelectedAgents.OrderBy(static package => package.Manifest.AgentName.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hostArtifacts = package.Manifest.HostArtifacts
                .Where(artifact => artifact.HostId == agentTarget.HostId)
                .ToArray();
            if (hostArtifacts.Length == 0)
            {
                diagnostics.Add(new AgentDoctorDiagnostic(
                    package.Manifest.AgentName,
                    AgentDoctorDiagnosticArea.HostArtifact,
                    isError: true,
                    AgentDistributionFailureCodes.HostUnsupported,
                    $"Agent has no generated artifacts for host '{Vocabulary.GetText(agentTarget.HostId)}'."));
                continue;
            }

            var stateResult = await targetInspector.InspectAsync(package.Manifest, agentTarget, cancellationToken).ConfigureAwait(false);
            if (!stateResult.IsSuccess)
            {
                diagnostics.Add(new AgentDoctorDiagnostic(
                    package.Manifest.AgentName,
                    AgentDoctorDiagnosticArea.TargetState,
                    isError: true,
                    stateResult.Failure!.Code,
                    stateResult.Failure.Message));
                continue;
            }

            var state = stateResult.Value!;
            diagnostics.Add(new AgentDoctorDiagnostic(
                package.Manifest.AgentName,
                AgentDoctorDiagnosticArea.TargetState,
                state.Kind != AgentInstalledTargetStateKind.Current,
                ResolveStateCode(state.Kind),
                state.Kind == AgentInstalledTargetStateKind.Current
                    ? "Custom-agent artifacts and ownership state are current."
                    : state.Detail ?? $"Custom-agent target state is {state.Kind}."));
        }

        var skillResult = await skillDoctorService.DiagnoseAsync(
            input.Catalog.ResolvedSkills,
            skillTargetResult.Value!.Host,
            skillTargetResult.Value.TargetRoot.Value,
            cancellationToken).ConfigureAwait(false);
        return AgentDistributionOperationResult<AgentDoctorResult>.Success(new AgentDoctorResult(
            agentTarget.ArtifactRoot,
            agentTarget.StateRoot,
            diagnostics,
            skillResult));
    }

    private static AgentDistributionFailureCode ResolveStateCode (AgentInstalledTargetStateKind kind)
    {
        return kind switch
        {
            AgentInstalledTargetStateKind.Current => HealthyCode,
            AgentInstalledTargetStateKind.Missing or AgentInstalledTargetStateKind.Unmanaged or AgentInstalledTargetStateKind.OtherCatalog => AgentDistributionFailureCodes.InstallTargetUnmanaged,
            AgentInstalledTargetStateKind.CleanOutdated => AgentDistributionFailureCodes.InstallTargetOutdated,
            AgentInstalledTargetStateKind.LocallyModified => AgentDistributionFailureCodes.InstallTargetLocalModification,
            _ => AgentDistributionFailureCodes.ManifestInvalid,
        };
    }

    private static AgentDistributionOperationResult<AgentDoctorResult> Failure (AgentDistributionFailure failure)
    {
        return AgentDistributionOperationResult<AgentDoctorResult>.FailureResult(failure.Code, failure.Message);
    }
}

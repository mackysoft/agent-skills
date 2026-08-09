using MackySoft.AgentSkills.Agents.Installation.State;
using MackySoft.AgentSkills.Agents.Installation.Targeting;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Doctor;

/// <summary> Diagnoses selected custom-agent packages, host artifacts, installed state, and resolved SKILL dependencies. </summary>
public sealed class AgentDoctorService
{
    private static readonly SkillFailureCode HealthyCode = new("AGENT_DOCTOR_OK");
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
    public async ValueTask<SkillOperationResult<AgentDoctorResult>> DiagnoseAsync (
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
            input.Catalog.BundleDescriptor.CatalogId);
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
                    SkillFailureCodes.HostUnsupported,
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
        return SkillOperationResult<AgentDoctorResult>.Success(new AgentDoctorResult(
            agentTarget.ArtifactRoot,
            agentTarget.StateRoot,
            diagnostics,
            skillResult));
    }

    private static SkillFailureCode ResolveStateCode (AgentInstalledTargetStateKind kind)
    {
        return kind switch
        {
            AgentInstalledTargetStateKind.Current => HealthyCode,
            AgentInstalledTargetStateKind.Missing or AgentInstalledTargetStateKind.Unmanaged or AgentInstalledTargetStateKind.OtherCatalog => SkillFailureCodes.InstallTargetUnmanaged,
            AgentInstalledTargetStateKind.CleanOutdated => SkillFailureCodes.InstallTargetOutdated,
            AgentInstalledTargetStateKind.LocallyModified => SkillFailureCodes.InstallTargetLocalModification,
            _ => SkillFailureCodes.ManifestInvalid,
        };
    }

    private static SkillOperationResult<AgentDoctorResult> Failure (SkillFailure failure)
    {
        return SkillOperationResult<AgentDoctorResult>.FailureResult(failure.Code, failure.Message);
    }
}

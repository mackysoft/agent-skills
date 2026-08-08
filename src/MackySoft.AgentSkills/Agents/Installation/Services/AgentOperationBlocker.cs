using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Installation.Services;

internal static class AgentOperationBlocker
{
    public static SkillOperationResult<bool> ValidateAgents (IReadOnlyList<AgentReconciliationPlan> plans)
    {
        var blocked = plans.FirstOrDefault(static plan => plan.Action.IsBlocked);
        return blocked is null
            ? SkillOperationResult<bool>.Success(true)
            : Failure(blocked.Action.AgentName, blocked.Action.TargetStateKind, blocked.Action.Detail);
    }

    public static SkillOperationResult<bool> ValidateSkills (SkillInstallResult result)
    {
        var blocked = result.Actions.FirstOrDefault(static action => action.ActionKind is
            SkillInstallActionKind.BlockedManagedOverwrite
            or SkillInstallActionKind.BlockedLocalModification
            or SkillInstallActionKind.BlockedUnmanaged);
        return blocked is null
            ? SkillOperationResult<bool>.Success(true)
            : SkillOperationResult<bool>.FailureResult(
                ResolveSkillFailureCode(blocked.ActionKind),
                $"Resolved SKILL dependency cannot be installed: {blocked.Identity.SkillName.Value} ({blocked.ActionKind}).");
    }

    public static SkillOperationResult<bool> ValidateSkills (SkillUpdateResult result)
    {
        var blocked = result.Actions.FirstOrDefault(static action => action.ActionKind is
            SkillUpdateActionKind.BlockedLocalModification
            or SkillUpdateActionKind.BlockedUnmanaged
            or SkillUpdateActionKind.BlockedVersionAhead);
        return blocked is null
            ? SkillOperationResult<bool>.Success(true)
            : SkillOperationResult<bool>.FailureResult(
                ResolveSkillFailureCode(blocked.ActionKind),
                $"Resolved SKILL dependency cannot be updated: {blocked.Identity.SkillName.Value} ({blocked.ActionKind}).");
    }

    private static SkillOperationResult<bool> Failure (AgentName agentName, State.AgentInstalledTargetStateKind kind, string? detail)
    {
        var code = kind switch
        {
            State.AgentInstalledTargetStateKind.LocallyModified => SkillFailureCodes.InstallTargetLocalModification,
            State.AgentInstalledTargetStateKind.Unmanaged => SkillFailureCodes.InstallTargetUnmanaged,
            State.AgentInstalledTargetStateKind.CleanOutdated => SkillFailureCodes.InstallTargetOutdated,
            State.AgentInstalledTargetStateKind.OtherCatalog => SkillFailureCodes.InstallTargetUnmanaged,
            _ => SkillFailureCodes.ManifestInvalid,
        };
        return SkillOperationResult<bool>.FailureResult(
            code,
            $"Custom agent '{agentName.Value}' cannot be reconciled from target state '{kind}'.{(string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}")}");
    }

    private static SkillFailureCode ResolveSkillFailureCode (SkillInstallActionKind kind)
    {
        return kind switch
        {
            SkillInstallActionKind.BlockedLocalModification => SkillFailureCodes.InstallTargetLocalModification,
            SkillInstallActionKind.BlockedUnmanaged => SkillFailureCodes.InstallTargetUnmanaged,
            _ => SkillFailureCodes.InstallTargetOutdated,
        };
    }

    private static SkillFailureCode ResolveSkillFailureCode (SkillUpdateActionKind kind)
    {
        return kind switch
        {
            SkillUpdateActionKind.BlockedLocalModification => SkillFailureCodes.InstallTargetLocalModification,
            SkillUpdateActionKind.BlockedUnmanaged => SkillFailureCodes.InstallTargetUnmanaged,
            _ => SkillFailureCodes.InstallTargetVersionAhead,
        };
    }
}

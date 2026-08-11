using MackySoft.AgentDistribution.Installation.Results;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Agents.Installation.Services;

internal static class AgentOperationBlocker
{
    public static AgentDistributionOperationResult<bool> ValidateAgents (IReadOnlyList<AgentReconciliationPlan> plans)
    {
        var blocked = plans.FirstOrDefault(static plan => plan.Action.IsBlocked);
        return blocked is null
            ? AgentDistributionOperationResult<bool>.Success(true)
            : Failure(blocked.Action.AgentName, blocked.Action.TargetStateKind, blocked.Action.Detail);
    }

    public static AgentDistributionOperationResult<bool> ValidateSkills (SkillInstallResult result)
    {
        var blocked = result.Actions.FirstOrDefault(static action => action.ActionKind is
            SkillInstallActionKind.BlockedManagedOverwrite
            or SkillInstallActionKind.BlockedLocalModification
            or SkillInstallActionKind.BlockedUnmanaged);
        return blocked is null
            ? AgentDistributionOperationResult<bool>.Success(true)
            : AgentDistributionOperationResult<bool>.FailureResult(
                ResolveAgentDistributionFailureCode(blocked.ActionKind),
                $"Resolved SKILL dependency cannot be installed: {blocked.Identity.SkillName.Value} ({blocked.ActionKind}).");
    }

    public static AgentDistributionOperationResult<bool> ValidateSkills (SkillUpdateResult result)
    {
        var blocked = result.Actions.FirstOrDefault(static action => action.ActionKind is
            SkillUpdateActionKind.BlockedLocalModification
            or SkillUpdateActionKind.BlockedUnmanaged
            or SkillUpdateActionKind.BlockedVersionAhead);
        return blocked is null
            ? AgentDistributionOperationResult<bool>.Success(true)
            : AgentDistributionOperationResult<bool>.FailureResult(
                ResolveAgentDistributionFailureCode(blocked.ActionKind),
                $"Resolved SKILL dependency cannot be updated: {blocked.Identity.SkillName.Value} ({blocked.ActionKind}).");
    }

    private static AgentDistributionOperationResult<bool> Failure (AgentName agentName, State.AgentInstalledTargetStateKind kind, string? detail)
    {
        var code = kind switch
        {
            State.AgentInstalledTargetStateKind.LocallyModified => AgentDistributionFailureCodes.InstallTargetLocalModification,
            State.AgentInstalledTargetStateKind.Unmanaged => AgentDistributionFailureCodes.InstallTargetUnmanaged,
            State.AgentInstalledTargetStateKind.CleanOutdated => AgentDistributionFailureCodes.InstallTargetOutdated,
            State.AgentInstalledTargetStateKind.OtherCatalog => AgentDistributionFailureCodes.InstallTargetUnmanaged,
            _ => AgentDistributionFailureCodes.ManifestInvalid,
        };
        return AgentDistributionOperationResult<bool>.FailureResult(
            code,
            $"Custom agent '{agentName.Value}' cannot be reconciled from target state '{kind}'.{(string.IsNullOrWhiteSpace(detail) ? string.Empty : $" {detail}")}");
    }

    private static AgentDistributionFailureCode ResolveAgentDistributionFailureCode (SkillInstallActionKind kind)
    {
        return kind switch
        {
            SkillInstallActionKind.BlockedLocalModification => AgentDistributionFailureCodes.InstallTargetLocalModification,
            SkillInstallActionKind.BlockedUnmanaged => AgentDistributionFailureCodes.InstallTargetUnmanaged,
            _ => AgentDistributionFailureCodes.InstallTargetOutdated,
        };
    }

    private static AgentDistributionFailureCode ResolveAgentDistributionFailureCode (SkillUpdateActionKind kind)
    {
        return kind switch
        {
            SkillUpdateActionKind.BlockedLocalModification => AgentDistributionFailureCodes.InstallTargetLocalModification,
            SkillUpdateActionKind.BlockedUnmanaged => AgentDistributionFailureCodes.InstallTargetUnmanaged,
            _ => AgentDistributionFailureCodes.InstallTargetVersionAhead,
        };
    }
}

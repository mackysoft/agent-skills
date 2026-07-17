using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents one per-skill prune action. </summary>
public sealed class SkillPruneAction
{
    /// <summary> Initializes one per-skill prune action. </summary>
    internal SkillPruneAction (
        SkillInstallIdentity identity,
        SkillPruneActionKind actionKind,
        SkillActionTargetState? targetState,
        SkillBlockedReason? blockedReason,
        SkillActionFileChanges? fileChanges)
    {
        Identity = SkillActionContractGuard.ValidateIdentity(identity, nameof(identity));
        ActionKind = SkillActionContractGuard.ValidateEnum(actionKind, nameof(actionKind));
        BlockedReason = blockedReason is null ? null : SkillActionContractGuard.ValidateEnum(blockedReason.Value, nameof(blockedReason));
        TargetState = targetState;
        FileChanges = fileChanges;

        ValidateCombination();
    }

    /// <summary> Gets the installed SKILL identity. </summary>
    public SkillInstallIdentity Identity { get; }

    /// <summary> Gets the prune action kind. </summary>
    public SkillPruneActionKind ActionKind { get; }

    /// <summary> Gets the reason for a blocked action, or <see langword="null" /> for other actions. </summary>
    public SkillBlockedReason? BlockedReason { get; }

    /// <summary> Gets the target state when the prune decision is based on inspected target content. </summary>
    public SkillActionTargetState? TargetState { get; }

    /// <summary>
    /// Gets the existing file removal summary for <see cref="SkillPruneActionKind.Deleted" /> actions.
    /// </summary>
    public SkillActionFileChanges? FileChanges { get; }

    private void ValidateCombination ()
    {
        switch (ActionKind)
        {
            case SkillPruneActionKind.Deleted:
                ValidateDeleted();
                return;
            case SkillPruneActionKind.SkippedCurrent:
            case SkillPruneActionKind.SkippedForeignCatalog:
                ValidateUninspectedSkip();
                return;
            case SkillPruneActionKind.SkippedUnmanaged:
                ValidateStateOnlyAction(SkillTargetStateKind.Unmanaged);
                return;
            case SkillPruneActionKind.BlockedLocalModification:
                var localTargetState = SkillActionContractGuard.RequireNotNull(TargetState, nameof(TargetState));
                SkillActionContractGuard.RequireTargetState(localTargetState, SkillTargetStateClassifier.IsLocalModificationDrift, nameof(TargetState), "A blocked prune action requires a locally modified target state.");
                if (BlockedReason != SkillBlockedReason.LocalModificationRequiresForce)
                {
                    throw new ArgumentException("A blocked prune action requires the local-modification blocked reason.", nameof(BlockedReason));
                }

                SkillActionContractGuard.RequireNull(FileChanges, nameof(FileChanges), "A blocked prune action must not contain file changes.");
                return;
            case SkillPruneActionKind.BlockedManifestInvalid:
                ValidateStateOnlyAction(SkillTargetStateKind.ManifestDrift);
                return;
            case SkillPruneActionKind.BlockedNameCollision:
                ValidateStateOnlyAction(SkillTargetStateKind.NameCollision);
                return;
            case SkillPruneActionKind.BlockedHostConflict:
                ValidateStateOnlyAction(SkillTargetStateKind.HostConflict);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(ActionKind), ActionKind, "Unsupported prune action kind.");
        }
    }

    private void ValidateDeleted ()
    {
        var targetState = SkillActionContractGuard.RequireNotNull(TargetState, nameof(TargetState));
        SkillActionContractGuard.RequireTargetState(
            targetState,
            static kind => kind == SkillTargetStateKind.RemovedFromCatalog || SkillTargetStateClassifier.IsLocalModificationDrift(kind),
            nameof(TargetState),
            "A deleted prune action requires a removed-from-catalog or locally modified target state.");
        SkillActionContractGuard.RequireNull(BlockedReason, nameof(BlockedReason), "A deleted prune action must not contain a blocked reason.");
        var fileChanges = SkillActionContractGuard.RequireNotNull(FileChanges, nameof(FileChanges));
        if (fileChanges.ReplacedFiles.Count != 0)
        {
            throw new ArgumentException("A deleted prune action must not contain replaced files.", nameof(FileChanges));
        }
    }

    private void ValidateUninspectedSkip ()
    {
        SkillActionContractGuard.RequireNull(TargetState, nameof(TargetState), "A prune action skipped without inspecting content must not contain a target state.");
        SkillActionContractGuard.RequireNull(BlockedReason, nameof(BlockedReason), "A skipped prune action must not contain a blocked reason.");
        SkillActionContractGuard.RequireNull(FileChanges, nameof(FileChanges), "A skipped prune action must not contain file changes.");
    }

    private void ValidateStateOnlyAction (SkillTargetStateKind expectedTargetState)
    {
        var targetState = SkillActionContractGuard.RequireNotNull(TargetState, nameof(TargetState));
        SkillActionContractGuard.RequireTargetState(targetState, kind => kind == expectedTargetState, nameof(TargetState), "The target state does not match the prune action.");
        SkillActionContractGuard.RequireNull(BlockedReason, nameof(BlockedReason), "This prune action must not contain a blocked reason.");
        SkillActionContractGuard.RequireNull(FileChanges, nameof(FileChanges), "This prune action must not contain file changes.");
    }
}

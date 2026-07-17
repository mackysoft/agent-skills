using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents one per-skill uninstall action. </summary>
public sealed class SkillUninstallAction
{
    /// <summary> Initializes one per-skill uninstall action. </summary>
    internal SkillUninstallAction (
        SkillInstallIdentity identity,
        SkillUninstallActionKind actionKind,
        SkillActionTargetState targetState,
        SkillBlockedReason? blockedReason,
        SkillActionFileChanges? fileChanges)
    {
        Identity = SkillActionContractGuard.ValidateIdentity(identity, nameof(identity));
        ActionKind = SkillActionContractGuard.ValidateEnum(actionKind, nameof(actionKind));
        TargetState = targetState ?? throw new ArgumentNullException(nameof(targetState));
        BlockedReason = blockedReason is null ? null : SkillActionContractGuard.ValidateEnum(blockedReason.Value, nameof(blockedReason));
        FileChanges = fileChanges;

        ValidateCombination();
    }

    /// <summary> Gets the installed SKILL identity. </summary>
    public SkillInstallIdentity Identity { get; }

    /// <summary> Gets the uninstall action kind. </summary>
    public SkillUninstallActionKind ActionKind { get; }

    /// <summary> Gets the reason for a blocked action, or <see langword="null" /> for non-blocked actions. </summary>
    public SkillBlockedReason? BlockedReason { get; }

    /// <summary> Gets the analyzed target state that caused this action. </summary>
    public SkillActionTargetState TargetState { get; }

    /// <summary>
    /// Gets the existing file removal summary for <see cref="SkillUninstallActionKind.Deleted" /> actions.
    /// </summary>
    /// <remarks>
    /// The value contains every existing file under the deleted skill directory and an empty replacement list.
    /// It is <see langword="null" /> for <see cref="SkillUninstallActionKind.NoOp" />,
    /// <see cref="SkillUninstallActionKind.SkippedUnmanaged" />, and blocked local modification actions.
    /// </remarks>
    public SkillActionFileChanges? FileChanges { get; }

    private void ValidateCombination ()
    {
        switch (ActionKind)
        {
            case SkillUninstallActionKind.Deleted:
                SkillActionContractGuard.RequireTargetState(TargetState, SkillActionContractGuard.IsReplacementTargetStateOrCurrent, nameof(TargetState), "A deleted uninstall action requires a managed target state.");
                SkillActionContractGuard.RequireNull(BlockedReason, nameof(BlockedReason), "A deleted uninstall action must not contain a blocked reason.");
                ValidateDeletionFileChanges();
                return;
            case SkillUninstallActionKind.NoOp:
                ValidateUnchangedAction(SkillTargetStateKind.Missing);
                return;
            case SkillUninstallActionKind.SkippedUnmanaged:
                ValidateUnchangedAction(SkillTargetStateKind.Unmanaged);
                return;
            case SkillUninstallActionKind.BlockedLocalModification:
                SkillActionContractGuard.RequireTargetState(TargetState, SkillTargetStateClassifier.IsLocalModificationDrift, nameof(TargetState), "A blocked uninstall action requires a locally modified target state.");
                if (BlockedReason != SkillBlockedReason.LocalModificationRequiresForce)
                {
                    throw new ArgumentException("A blocked uninstall action requires the local-modification blocked reason.", nameof(BlockedReason));
                }

                SkillActionContractGuard.RequireNull(FileChanges, nameof(FileChanges), "A blocked uninstall action must not contain file changes.");
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(ActionKind), ActionKind, "Unsupported uninstall action kind.");
        }
    }

    private void ValidateUnchangedAction (SkillTargetStateKind expectedTargetState)
    {
        SkillActionContractGuard.RequireTargetState(TargetState, kind => kind == expectedTargetState, nameof(TargetState), "The target state does not match the unchanged uninstall action.");
        SkillActionContractGuard.RequireNull(BlockedReason, nameof(BlockedReason), "An unchanged uninstall action must not contain a blocked reason.");
        SkillActionContractGuard.RequireNull(FileChanges, nameof(FileChanges), "An unchanged uninstall action must not contain file changes.");
    }

    private void ValidateDeletionFileChanges ()
    {
        var fileChanges = SkillActionContractGuard.RequireNotNull(FileChanges, nameof(FileChanges));
        if (fileChanges.ReplacedFiles.Count != 0)
        {
            throw new ArgumentException("A deleted uninstall action must not contain replaced files.", nameof(FileChanges));
        }
    }
}

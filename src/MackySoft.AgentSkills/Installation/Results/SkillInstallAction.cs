using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents one per-skill install action. </summary>
public sealed class SkillInstallAction
{
    /// <summary> Initializes one per-skill install action. </summary>
    internal SkillInstallAction (
        SkillInstallIdentity identity,
        SkillInstallActionKind actionKind,
        SkillActionTargetState targetState,
        SkillBlockedReason? blockedReason,
        IReadOnlyList<SkillActionDiff>? diffs,
        SkillActionFileChanges? fileChanges)
    {
        Identity = SkillActionContractGuard.ValidateIdentity(identity, nameof(identity));
        ActionKind = SkillActionContractGuard.ValidateEnum(actionKind, nameof(actionKind));
        TargetState = targetState ?? throw new ArgumentNullException(nameof(targetState));
        BlockedReason = blockedReason is null
            ? null
            : SkillActionContractGuard.ValidateEnum(blockedReason.Value, nameof(blockedReason));
        Diffs = SkillActionContractGuard.OptionalSnapshot(diffs, nameof(diffs));
        FileChanges = fileChanges;

        ValidateCombination();
    }

    /// <summary> Gets the installed SKILL identity. </summary>
    public SkillInstallIdentity Identity { get; }

    /// <summary> Gets the install action kind. </summary>
    public SkillInstallActionKind ActionKind { get; }

    /// <summary> Gets the reason for a blocked action, or <see langword="null" /> for non-blocked actions. </summary>
    public SkillBlockedReason? BlockedReason { get; }

    /// <summary> Gets structured diffs for changed or blocked actions, or <see langword="null" /> for no-op actions. </summary>
    public IReadOnlyList<SkillActionDiff>? Diffs { get; }

    /// <summary> Gets the analyzed target state that caused this action. </summary>
    public SkillActionTargetState TargetState { get; }

    /// <summary>
    /// Gets the existing file change summary for <see cref="SkillInstallActionKind.Created" /> and
    /// <see cref="SkillInstallActionKind.Updated" /> actions.
    /// </summary>
    /// <remarks>
    /// The value is an empty summary for <see cref="SkillInstallActionKind.Created" /> and the planned or
    /// performed replacement summary for <see cref="SkillInstallActionKind.Updated" />. It is
    /// <see langword="null" /> for <see cref="SkillInstallActionKind.NoOp" />, blocked managed overwrite,
    /// blocked local modification, and blocked unmanaged actions.
    /// </remarks>
    public SkillActionFileChanges? FileChanges { get; }

    private void ValidateCombination ()
    {
        switch (ActionKind)
        {
            case SkillInstallActionKind.Created:
                ValidateChangedAction(static kind => kind == SkillTargetStateKind.Missing, requireEmptyFileChanges: true);
                return;
            case SkillInstallActionKind.Updated:
                ValidateChangedAction(SkillActionContractGuard.IsReplacementTargetState, requireEmptyFileChanges: false);
                return;
            case SkillInstallActionKind.NoOp:
                SkillActionContractGuard.RequireTargetState(
                    TargetState,
                    static kind => kind == SkillTargetStateKind.Current,
                    nameof(TargetState),
                    "A no-op install action requires a current target state.");
                SkillActionContractGuard.RequireNull(BlockedReason, nameof(BlockedReason), "A no-op install action must not contain a blocked reason.");
                SkillActionContractGuard.RequireNull(Diffs, nameof(Diffs), "A no-op install action must not contain diffs.");
                SkillActionContractGuard.RequireNull(FileChanges, nameof(FileChanges), "A no-op install action must not contain file changes.");
                return;
            case SkillInstallActionKind.BlockedManagedOverwrite:
                ValidateBlockedManagedOverwrite();
                return;
            case SkillInstallActionKind.BlockedLocalModification:
                ValidateBlockedAction(
                    SkillBlockedReason.LocalModificationRequiresForce,
                    SkillTargetStateClassifier.IsLocalModificationDrift);
                return;
            case SkillInstallActionKind.BlockedUnmanaged:
                ValidateBlockedAction(
                    SkillBlockedReason.UnmanagedTarget,
                    static kind => kind == SkillTargetStateKind.Unmanaged);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(ActionKind), ActionKind, "Unsupported install action kind.");
        }
    }

    private void ValidateChangedAction (
        Func<SkillTargetStateKind, bool> targetStatePredicate,
        bool requireEmptyFileChanges)
    {
        SkillActionContractGuard.RequireTargetState(TargetState, targetStatePredicate, nameof(TargetState), "The target state does not match the changed install action.");
        SkillActionContractGuard.RequireNull(BlockedReason, nameof(BlockedReason), "A changed install action must not contain a blocked reason.");
        SkillActionContractGuard.RequireNotNull(Diffs, nameof(Diffs));
        var fileChanges = SkillActionContractGuard.RequireNotNull(FileChanges, nameof(FileChanges));
        if (requireEmptyFileChanges
            && (fileChanges.ReplacedFiles.Count != 0 || fileChanges.RemovedFiles.Count != 0))
        {
            throw new ArgumentException("A created install action must contain an empty existing-file change summary.", nameof(FileChanges));
        }
    }

    private void ValidateBlockedManagedOverwrite ()
    {
        SkillActionContractGuard.RequireTargetState(
            TargetState,
            static kind => kind is SkillTargetStateKind.CleanOutdated or SkillTargetStateKind.VersionAhead,
            nameof(TargetState),
            "A blocked managed overwrite requires a clean-outdated or version-ahead target state.");
        var expectedReason = TargetState.Kind == SkillTargetStateKind.VersionAhead
            ? SkillBlockedReason.InstalledVersionAhead
            : SkillBlockedReason.ManagedOverwriteRequiresForce;
        ValidateBlockedAction(expectedReason, static kind => kind is SkillTargetStateKind.CleanOutdated or SkillTargetStateKind.VersionAhead);
    }

    private void ValidateBlockedAction (
        SkillBlockedReason expectedReason,
        Func<SkillTargetStateKind, bool> targetStatePredicate)
    {
        SkillActionContractGuard.RequireTargetState(TargetState, targetStatePredicate, nameof(TargetState), "The target state does not match the blocked install action.");
        if (BlockedReason != expectedReason)
        {
            throw new ArgumentException("The blocked reason does not match the install action kind and target state.", nameof(BlockedReason));
        }

        SkillActionContractGuard.RequireNotNull(Diffs, nameof(Diffs));
        SkillActionContractGuard.RequireNull(FileChanges, nameof(FileChanges), "A blocked install action must not contain file changes.");
    }
}

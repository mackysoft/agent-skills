namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents a planned or completed SKILL prune operation. </summary>
public sealed class SkillPruneResult
{
    /// <summary> Initializes one planned or completed prune operation. </summary>
    internal SkillPruneResult (
        string targetRoot,
        IReadOnlyList<SkillPruneAction> actions,
        bool dryRun,
        bool force)
    {
        TargetRoot = SkillActionContractGuard.ValidateTargetRoot(targetRoot, nameof(targetRoot));
        Actions = SkillActionContractGuard.Snapshot(actions, nameof(actions));
        foreach (var action in Actions)
        {
            SkillActionContractGuard.ValidateTargetRootMatchesIdentity(TargetRoot, action.Identity, nameof(actions));
        }

        DryRun = dryRun;
        Force = force;
    }

    /// <summary> Gets the canonical absolute bundle target root. </summary>
    public string TargetRoot { get; }

    /// <summary> Gets an immutable snapshot of per-SKILL prune actions. </summary>
    public IReadOnlyList<SkillPruneAction> Actions { get; }

    /// <summary> Gets whether the operation returned a plan without deletions. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether prune force semantics were enabled. </summary>
    public bool Force { get; }
}

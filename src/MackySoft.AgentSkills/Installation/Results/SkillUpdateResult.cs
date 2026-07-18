namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents a planned or completed SKILL update operation. </summary>
public sealed class SkillUpdateResult
{
    /// <summary> Initializes one planned or completed update operation. </summary>
    internal SkillUpdateResult (
        string targetRoot,
        IReadOnlyList<SkillUpdateAction> actions,
        bool dryRun,
        bool force,
        bool printDiff)
    {
        TargetRoot = SkillActionContractGuard.ValidateTargetRoot(targetRoot, nameof(targetRoot));
        Actions = SkillActionContractGuard.Snapshot(actions, nameof(actions));
        foreach (var action in Actions)
        {
            SkillActionContractGuard.ValidateTargetRootMatchesIdentity(TargetRoot, action.Identity, nameof(actions));
        }

        DryRun = dryRun;
        Force = force;
        PrintDiff = printDiff;
    }

    /// <summary> Gets the canonical absolute bundle target root. </summary>
    public string TargetRoot { get; }

    /// <summary> Gets an immutable snapshot of per-SKILL update actions. </summary>
    public IReadOnlyList<SkillUpdateAction> Actions { get; }

    /// <summary> Gets whether the result represents a plan without writes. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether update force semantics were enabled. </summary>
    public bool Force { get; }

    /// <summary> Gets whether structured diff payloads were requested. </summary>
    public bool PrintDiff { get; }
}

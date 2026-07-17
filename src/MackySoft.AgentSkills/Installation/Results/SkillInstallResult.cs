namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents a planned or completed SKILL install operation. </summary>
public sealed class SkillInstallResult
{
    /// <summary> Initializes one planned or completed install operation. </summary>
    internal SkillInstallResult (
        string targetRoot,
        IReadOnlyList<SkillInstallAction> actions,
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

    /// <summary> Gets the canonical absolute target root. </summary>
    public string TargetRoot { get; }

    /// <summary> Gets an immutable snapshot of per-SKILL install actions. </summary>
    public IReadOnlyList<SkillInstallAction> Actions { get; }

    /// <summary> Gets whether the result represents a plan without writes. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether install force semantics were enabled. </summary>
    public bool Force { get; }

    /// <summary> Gets whether structured diff payloads were requested. </summary>
    public bool PrintDiff { get; }
}

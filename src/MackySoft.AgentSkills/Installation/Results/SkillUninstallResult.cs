namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents a planned or completed SKILL uninstall operation. </summary>
public sealed class SkillUninstallResult
{
    /// <summary> Initializes one planned or completed uninstall operation. </summary>
    internal SkillUninstallResult (
        string targetRoot,
        IReadOnlyList<SkillUninstallAction> actions,
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

    /// <summary> Gets an immutable snapshot of per-SKILL uninstall actions. </summary>
    public IReadOnlyList<SkillUninstallAction> Actions { get; }

    /// <summary> Gets whether the result represents a plan without writes. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether uninstall force semantics were enabled. </summary>
    public bool Force { get; }
}

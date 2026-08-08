using MackySoft.AgentSkills.Installation.Results;

namespace MackySoft.AgentSkills.Agents.Installation.Results;

/// <summary> Represents a planned or completed custom-agent install and its SKILL dependency operation. </summary>
public sealed class AgentInstallResult
{
    /// <summary> Initializes one immutable install result. </summary>
    internal AgentInstallResult (string artifactRoot, string stateRoot, IReadOnlyList<AgentReconcileAction> actions, SkillInstallResult skillResult, bool dryRun, bool force, bool printDiff)
    {
        ArtifactRoot = AgentResultContractGuard.NormalizeAbsolutePath(artifactRoot, nameof(artifactRoot));
        StateRoot = AgentResultContractGuard.NormalizeAbsolutePath(stateRoot, nameof(stateRoot));
        Actions = Array.AsReadOnly(actions.ToArray());
        SkillResult = skillResult ?? throw new ArgumentNullException(nameof(skillResult));
        DryRun = dryRun;
        Force = force;
        PrintDiff = printDiff;
    }

    /// <summary> Gets the host-discovered artifact root. </summary>
    public string ArtifactRoot { get; }

    /// <summary> Gets the Agent Skills ownership-state root. </summary>
    public string StateRoot { get; }

    /// <summary> Gets per-agent outcomes. </summary>
    public IReadOnlyList<AgentReconcileAction> Actions { get; }

    /// <summary> Gets the distinct SKILL dependency operation result. </summary>
    public SkillInstallResult SkillResult { get; }

    /// <summary> Gets whether the result is a write-free plan. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether force semantics were enabled. </summary>
    public bool Force { get; }

    /// <summary> Gets whether SKILL diffs were requested. </summary>
    public bool PrintDiff { get; }
}

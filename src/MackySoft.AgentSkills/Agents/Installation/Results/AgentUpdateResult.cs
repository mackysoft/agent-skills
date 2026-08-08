using MackySoft.AgentSkills.Installation.Results;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Installation.Results;

/// <summary> Represents a planned or completed custom-agent update and its SKILL dependency operation. </summary>
public sealed class AgentUpdateResult
{
    /// <summary> Initializes one immutable update result. </summary>
    internal AgentUpdateResult (AbsolutePath artifactRoot, AbsolutePath stateRoot, IReadOnlyList<AgentReconcileAction> actions, SkillUpdateResult skillResult, bool dryRun, bool force, bool printDiff)
    {
        ArtifactRoot = artifactRoot ?? throw new ArgumentNullException(nameof(artifactRoot));
        StateRoot = stateRoot ?? throw new ArgumentNullException(nameof(stateRoot));
        Actions = Array.AsReadOnly(actions.ToArray());
        SkillResult = skillResult ?? throw new ArgumentNullException(nameof(skillResult));
        DryRun = dryRun;
        Force = force;
        PrintDiff = printDiff;
    }

    /// <summary> Gets the host-discovered artifact root. </summary>
    public AbsolutePath ArtifactRoot { get; }

    /// <summary> Gets the Agent Skills ownership-state root. </summary>
    public AbsolutePath StateRoot { get; }

    /// <summary> Gets per-agent outcomes. </summary>
    public IReadOnlyList<AgentReconcileAction> Actions { get; }

    /// <summary> Gets the distinct SKILL dependency operation result. </summary>
    public SkillUpdateResult SkillResult { get; }

    /// <summary> Gets whether the result is a write-free plan. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether force semantics were enabled. </summary>
    public bool Force { get; }

    /// <summary> Gets whether SKILL diffs were requested. </summary>
    public bool PrintDiff { get; }
}

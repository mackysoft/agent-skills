using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Installation.Results;

/// <summary> Represents a planned or completed custom-agent uninstall. </summary>
public sealed class AgentUninstallResult
{
    /// <summary> Initializes one immutable uninstall result. </summary>
    internal AgentUninstallResult (AbsolutePath artifactRoot, AbsolutePath stateRoot, IReadOnlyList<AgentRemovalAction> actions, bool dryRun, bool force)
    {
        ArtifactRoot = artifactRoot ?? throw new ArgumentNullException(nameof(artifactRoot));
        StateRoot = stateRoot ?? throw new ArgumentNullException(nameof(stateRoot));
        Actions = Array.AsReadOnly(actions.ToArray());
        DryRun = dryRun;
        Force = force;
    }

    /// <summary> Gets the host-discovered artifact root. </summary>
    public AbsolutePath ArtifactRoot { get; }

    /// <summary> Gets the Agent Skills ownership-state root. </summary>
    public AbsolutePath StateRoot { get; }

    /// <summary> Gets per-agent outcomes. No SKILL removal outcome exists by contract. </summary>
    public IReadOnlyList<AgentRemovalAction> Actions { get; }

    /// <summary> Gets whether the result is a deletion-free plan. </summary>
    public bool DryRun { get; }

    /// <summary> Gets whether force semantics were enabled. </summary>
    public bool Force { get; }
}

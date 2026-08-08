using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Installation.Targeting;

/// <summary> Represents resolved user-scope agent artifact and state roots. </summary>
public sealed class AgentUserTargetRoots
{
    /// <summary> Initializes resolved user-scope roots. </summary>
    internal AgentUserTargetRoots (AbsolutePath artifactRoot, AbsolutePath stateRoot)
    {
        ArtifactRoot = artifactRoot ?? throw new ArgumentNullException(nameof(artifactRoot));
        StateRoot = stateRoot ?? throw new ArgumentNullException(nameof(stateRoot));
    }

    /// <summary> Gets the host-discovered artifact root. </summary>
    public AbsolutePath ArtifactRoot { get; }

    /// <summary> Gets the host-unobserved installation-state root. </summary>
    public AbsolutePath StateRoot { get; }
}

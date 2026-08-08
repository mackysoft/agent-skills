using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Installation.Targeting;

/// <summary> Represents resolved user-scope agent artifact and state roots. </summary>
public sealed class AgentUserTargetRoots
{
    /// <summary> Initializes resolved user-scope roots. </summary>
    internal AgentUserTargetRoots (string artifactRoot, string stateRoot)
    {
        ArtifactRoot = AbsolutePath.Parse(artifactRoot).Value;
        StateRoot = AbsolutePath.Parse(stateRoot).Value;
    }

    /// <summary> Gets the host-discovered artifact root. </summary>
    public string ArtifactRoot { get; }

    /// <summary> Gets the host-unobserved installation-state root. </summary>
    public string StateRoot { get; }
}

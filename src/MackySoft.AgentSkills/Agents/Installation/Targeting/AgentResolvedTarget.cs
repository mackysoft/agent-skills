using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Installation.Targeting;

/// <summary> Represents one resolved custom-agent artifact and installation-state target. </summary>
public sealed class AgentResolvedTarget
{
    /// <summary> Initializes one resolved target. </summary>
    internal AgentResolvedTarget (
        HostKind hostId,
        AgentInstallScopeKind scope,
        AbsolutePath? repositoryRoot,
        AbsolutePath artifactRoot,
        AbsolutePath stateRoot)
    {
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }
        HostId = hostId;
        Scope = scope;
        RepositoryRoot = repositoryRoot;
        ArtifactRoot = artifactRoot ?? throw new ArgumentNullException(nameof(artifactRoot));
        StateRoot = stateRoot ?? throw new ArgumentNullException(nameof(stateRoot));
    }

    /// <summary> Gets the selected host identifier. </summary>
    public HostKind HostId { get; }

    /// <summary> Gets the selected installation scope. </summary>
    public AgentInstallScopeKind Scope { get; }

    /// <summary> Gets the project repository root, or <see langword="null" /> for user scope. </summary>
    public AbsolutePath? RepositoryRoot { get; }

    /// <summary> Gets the host-discovered agent artifact root. </summary>
    public AbsolutePath ArtifactRoot { get; }

    /// <summary> Gets the host-unobserved Agent Skills installation-state root. </summary>
    public AbsolutePath StateRoot { get; }
}

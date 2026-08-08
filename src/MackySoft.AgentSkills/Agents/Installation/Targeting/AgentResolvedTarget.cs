using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Installation.Targeting;

/// <summary> Represents one resolved custom-agent artifact and installation-state target. </summary>
public sealed class AgentResolvedTarget
{
    /// <summary> Initializes one resolved target. </summary>
    internal AgentResolvedTarget (AgentHostKind hostId, AgentInstallScopeKind scope, string? repositoryRoot, string artifactRoot, string stateRoot)
    {
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }
        if (!AbsolutePath.TryParse(artifactRoot, out var artifactPath, out _)
            || !AbsolutePath.TryParse(stateRoot, out var statePath, out _))
        {
            throw new ArgumentException("Resolved agent target paths must be absolute.");
        }

        HostId = hostId;
        Scope = scope;
        RepositoryRoot = repositoryRoot;
        ArtifactRoot = artifactPath.Value;
        StateRoot = statePath.Value;
    }

    /// <summary> Gets the selected host identifier. </summary>
    public AgentHostKind HostId { get; }

    /// <summary> Gets the selected installation scope. </summary>
    public AgentInstallScopeKind Scope { get; }

    /// <summary> Gets the project repository root, or <see langword="null" /> for user scope. </summary>
    public string? RepositoryRoot { get; }

    /// <summary> Gets the host-discovered agent artifact root. </summary>
    public string ArtifactRoot { get; }

    /// <summary> Gets the host-unobserved Agent Skills installation-state root. </summary>
    public string StateRoot { get; }
}

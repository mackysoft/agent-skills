using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Installation.Targeting;

/// <summary> Represents one custom-agent artifact target request. </summary>
public sealed class AgentTargetRequest
{
    /// <summary> Initializes a target request from already parsed path contracts. </summary>
    /// <param name="hostId"> The custom-agent host selected for the operation. </param>
    /// <param name="scope"> The installation scope. </param>
    /// <param name="repositoryRoot"> The absolute repository root required for project scope; <see langword="null" /> for user scope. </param>
    /// <param name="artifactTargetRoot"> The optional absolute artifact root. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="hostId" /> or <paramref name="scope" /> is not defined. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when project scope is selected without <paramref name="repositoryRoot" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when user scope is selected with <paramref name="repositoryRoot" />. </exception>
    public AgentTargetRequest (HostKind hostId, AgentInstallScopeKind scope, AbsolutePath? repositoryRoot, AbsolutePath? artifactTargetRoot = null)
    {
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }
        if (!Vocabulary.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported agent install scope.");
        }

        if (scope == AgentInstallScopeKind.Project)
        {
            ArgumentNullException.ThrowIfNull(repositoryRoot);
        }
        else if (repositoryRoot is not null)
        {
            throw new ArgumentException("User-scope agent target request must not contain a repository root.", nameof(repositoryRoot));
        }

        HostId = hostId;
        Scope = scope;
        RepositoryRoot = repositoryRoot;
        ArtifactTargetRoot = artifactTargetRoot;
    }

    /// <summary> Gets the requested host identifier. </summary>
    public HostKind HostId { get; }

    /// <summary> Gets the installation scope. </summary>
    public AgentInstallScopeKind Scope { get; }

    /// <summary> Gets the canonical absolute project repository root, or <see langword="null" /> for user scope. </summary>
    public AbsolutePath? RepositoryRoot { get; }

    /// <summary> Gets the optional canonical absolute artifact root override after repository-relative resolution. </summary>
    public AbsolutePath? ArtifactTargetRoot { get; }
}

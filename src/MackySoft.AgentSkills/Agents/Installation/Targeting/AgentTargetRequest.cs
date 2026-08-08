using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Installation.Targeting;

/// <summary> Represents one custom-agent artifact target request. </summary>
public sealed class AgentTargetRequest
{
    /// <summary> Initializes a target request and resolves its path inputs into guarded values. </summary>
    /// <param name="hostId"> The custom-agent host selected for the operation. </param>
    /// <param name="scope"> The installation scope. </param>
    /// <param name="repositoryRoot"> The absolute repository root required for project scope; <see langword="null" /> for user scope. </param>
    /// <param name="artifactTargetRoot">
    /// The optional exact artifact root. Project scope accepts an absolute path or a path relative to
    /// <paramref name="repositoryRoot" />; user scope accepts an absolute path only.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="hostId" /> or <paramref name="scope" /> is not defined. </exception>
    /// <exception cref="ArgumentException"> Thrown when a path does not satisfy the selected scope contract. </exception>
    public AgentTargetRequest (AgentHostKind hostId, AgentInstallScopeKind scope, string? repositoryRoot, string? artifactTargetRoot = null)
    {
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }
        if (!Vocabulary.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported agent install scope.");
        }

        AbsolutePath? normalizedRepositoryRoot = null;
        if (scope == AgentInstallScopeKind.Project)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
            if (!AbsolutePath.TryParse(repositoryRoot, out normalizedRepositoryRoot, out var repositoryFailure))
            {
                throw new ArgumentException($"Project-scope repository root must be an absolute path: {repositoryFailure.Message}", nameof(repositoryRoot));
            }
        }
        else if (repositoryRoot is not null)
        {
            throw new ArgumentException("User-scope agent target request must not contain a repository root.", nameof(repositoryRoot));
        }

        AbsolutePath? normalizedArtifactTargetRoot = null;
        if (artifactTargetRoot is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(artifactTargetRoot);
            if (AbsolutePath.TryParse(artifactTargetRoot, out var absoluteArtifactRoot, out var artifactFailure))
            {
                normalizedArtifactTargetRoot = absoluteArtifactRoot;
            }
            else if (scope == AgentInstallScopeKind.User)
            {
                throw new ArgumentException($"User-scope artifact target root must be an absolute path: {artifactFailure.Message}", nameof(artifactTargetRoot));
            }
            else if (RootRelativePath.TryParse(artifactTargetRoot, out var relativeArtifactRoot, out var relativeFailure))
            {
                normalizedArtifactTargetRoot = ContainedPath.Create(normalizedRepositoryRoot!, relativeArtifactRoot).Target;
            }
            else
            {
                throw new ArgumentException($"Project-scope artifact target root must be absolute or repository-relative: {relativeFailure.Message}", nameof(artifactTargetRoot));
            }
        }

        HostId = hostId;
        Scope = scope;
        RepositoryRoot = normalizedRepositoryRoot;
        ArtifactTargetRoot = normalizedArtifactTargetRoot;
    }

    /// <summary> Gets the requested host identifier. </summary>
    public AgentHostKind HostId { get; }

    /// <summary> Gets the installation scope. </summary>
    public AgentInstallScopeKind Scope { get; }

    /// <summary> Gets the canonical absolute project repository root, or <see langword="null" /> for user scope. </summary>
    public AbsolutePath? RepositoryRoot { get; }

    /// <summary> Gets the optional canonical absolute artifact root override after repository-relative resolution. </summary>
    public AbsolutePath? ArtifactTargetRoot { get; }
}

using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Installation.Targeting;

/// <summary> Represents one SKILL install target request. </summary>
public sealed class SkillInstallRequest
{
    /// <summary> Initializes one SKILL install target request. </summary>
    /// <param name="host"> The target host. </param>
    /// <param name="scope"> The install scope. </param>
    /// <param name="repositoryRoot"> The absolute repository root required for project scope; <see langword="null" /> for user scope. </param>
    /// <param name="targetRoot"> The optional absolute bundle target root. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="host" /> or <paramref name="scope" /> is unsupported. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when project scope is selected without <paramref name="repositoryRoot" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when user scope is selected with <paramref name="repositoryRoot" />. </exception>
    public SkillInstallRequest (
        HostKind host,
        SkillScopeKind scope,
        AbsolutePath? repositoryRoot,
        AbsolutePath? targetRoot = null)
    {
        if (!Vocabulary.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        if (!Vocabulary.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported SKILL install scope.");
        }

        if (scope == SkillScopeKind.Project)
        {
            ArgumentNullException.ThrowIfNull(repositoryRoot);
        }
        else if (repositoryRoot is not null)
        {
            throw new ArgumentException("User-scope install request must not contain a repository root.", nameof(repositoryRoot));
        }

        Host = host;
        Scope = scope;
        RepositoryRoot = repositoryRoot;
        TargetRoot = targetRoot;
    }

    /// <summary> Gets the target host. </summary>
    public HostKind Host { get; }

    /// <summary> Gets the install scope. </summary>
    public SkillScopeKind Scope { get; }

    /// <summary> Gets the canonical absolute repository root for project scope, or <see langword="null" /> for user scope. </summary>
    public AbsolutePath? RepositoryRoot { get; }

    /// <summary>
    /// Gets the optional explicit bundle target root. The host-specific catalog-directory layout is applied only when
    /// this value is <see langword="null" />. User-scope roots are canonical and absolute.
    /// </summary>
    public AbsolutePath? TargetRoot { get; }
}

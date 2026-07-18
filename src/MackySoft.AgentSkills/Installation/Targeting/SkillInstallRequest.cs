using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Installation.Targeting;

/// <summary> Represents one SKILL install target request. </summary>
public sealed class SkillInstallRequest
{
    /// <summary> Initializes one SKILL install target request. </summary>
    /// <param name="host"> The target host. </param>
    /// <param name="scope"> The install scope. </param>
    /// <param name="repositoryRoot"> The canonical absolute repository root required for project scope; <see langword="null" /> for user scope. </param>
    /// <param name="targetRoot"> The optional explicit bundle target root. User-scope roots must be absolute. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="host" /> or <paramref name="scope" /> is unsupported. </exception>
    /// <exception cref="ArgumentException"> Thrown when the paths do not satisfy the selected scope contract. </exception>
    public SkillInstallRequest (
        SkillHostKind host,
        SkillScopeKind scope,
        string? repositoryRoot,
        string? targetRoot = null)
    {
        if (!ContractLiteralCodec.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        if (!ContractLiteralCodec.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported SKILL install scope.");
        }

        string? normalizedRepositoryRoot;
        string? normalizedTargetRoot;
        if (scope == SkillScopeKind.Project)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
            if (!Path.IsPathFullyQualified(repositoryRoot))
            {
                throw new ArgumentException("Project-scope repository root must be an absolute path.", nameof(repositoryRoot));
            }

            normalizedRepositoryRoot = Path.GetFullPath(repositoryRoot);
            normalizedTargetRoot = NormalizeProjectTargetRoot(targetRoot);
        }
        else
        {
            if (repositoryRoot is not null)
            {
                throw new ArgumentException("User-scope install request must not contain a repository root.", nameof(repositoryRoot));
            }

            normalizedRepositoryRoot = null;
            normalizedTargetRoot = NormalizeUserTargetRoot(targetRoot);
        }

        Host = host;
        Scope = scope;
        RepositoryRoot = normalizedRepositoryRoot;
        TargetRoot = normalizedTargetRoot;
    }

    /// <summary> Gets the target host. </summary>
    public SkillHostKind Host { get; }

    /// <summary> Gets the install scope. </summary>
    public SkillScopeKind Scope { get; }

    /// <summary> Gets the canonical absolute repository root for project scope, or <see langword="null" /> for user scope. </summary>
    public string? RepositoryRoot { get; }

    /// <summary>
    /// Gets the optional explicit bundle target root. The host-specific catalog-directory layout is applied only when
    /// this value is <see langword="null" />. User-scope roots are canonical and absolute.
    /// </summary>
    public string? TargetRoot { get; }

    private static string? NormalizeProjectTargetRoot (string? targetRoot)
    {
        if (targetRoot is null)
        {
            return null;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        return Path.IsPathFullyQualified(targetRoot)
            ? Path.GetFullPath(targetRoot)
            : targetRoot;
    }

    private static string? NormalizeUserTargetRoot (string? targetRoot)
    {
        if (targetRoot is null)
        {
            return null;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        if (!Path.IsPathFullyQualified(targetRoot))
        {
            throw new ArgumentException("User-scope target root must be an absolute path.", nameof(targetRoot));
        }

        return Path.GetFullPath(targetRoot);
    }
}

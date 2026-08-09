using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.FileSystem;
using MackySoft.Text.Vocabularies;

namespace MackySoft.AgentDistribution.Hosting.Commands;

/// <summary>Associates one parsed command scope with the repository root required by that scope.</summary>
internal sealed class CommandRepositoryContext
{
    public CommandRepositoryContext (SkillScopeKind scope, AbsolutePath? repositoryRoot)
    {
        if (!Vocabulary.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported command scope.");
        }

        if ((scope == SkillScopeKind.Project) != (repositoryRoot is not null))
        {
            throw new ArgumentException("Project scope requires a repository root and user scope forbids one.", nameof(repositoryRoot));
        }

        Scope = scope;
        RepositoryRoot = repositoryRoot;
    }

    public SkillScopeKind Scope { get; }

    public AbsolutePath? RepositoryRoot { get; }
}

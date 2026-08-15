using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Targeting;

/// <summary> Represents a resolved bundle install target. </summary>
public sealed class SkillResolvedInstallTarget
{
    /// <summary> Initializes one resolved bundle install target. </summary>
    /// <param name="host"> The resolved host. </param>
    /// <param name="scope"> The canonical install scope. </param>
    /// <param name="targetRoot"> The canonical absolute bundle target root. </param>
    internal SkillResolvedInstallTarget (
        SkillResolvedHost host,
        SkillScopeKind scope,
        AbsolutePath targetRoot)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!Vocabulary.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported SKILL install scope.");
        }

        ResolvedHost = host;
        Scope = scope;
        TargetRoot = targetRoot ?? throw new ArgumentNullException(nameof(targetRoot));
    }

    /// <summary> Gets the canonical host. </summary>
    public HostKind Host => ResolvedHost.Host;

    /// <summary> Gets the resolved host information. </summary>
    public SkillResolvedHost ResolvedHost { get; }

    /// <summary> Gets the canonical install scope. </summary>
    public SkillScopeKind Scope { get; }

    /// <summary> Gets the canonical absolute bundle target root. </summary>
    public AbsolutePath TargetRoot { get; }
}

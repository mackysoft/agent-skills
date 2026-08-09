using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Targeting;

/// <summary> Represents a resolved bundle install target. </summary>
public sealed class SkillResolvedInstallTarget
{
    /// <summary> Initializes one resolved bundle install target. </summary>
    /// <param name="host"> The canonical host. </param>
    /// <param name="targetRoot"> The canonical absolute bundle target root. </param>
    internal SkillResolvedInstallTarget (
        HostKind host,
        AbsolutePath targetRoot)
    {
        if (!Vocabulary.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        Host = host;
        TargetRoot = targetRoot ?? throw new ArgumentNullException(nameof(targetRoot));
    }

    /// <summary> Gets the canonical host. </summary>
    public HostKind Host { get; }

    /// <summary> Gets the canonical absolute bundle target root. </summary>
    public AbsolutePath TargetRoot { get; }
}

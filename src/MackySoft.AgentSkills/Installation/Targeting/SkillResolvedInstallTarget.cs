using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Installation.Targeting;

/// <summary> Represents a resolved bundle install target. </summary>
public sealed class SkillResolvedInstallTarget
{
    /// <summary> Initializes one resolved bundle install target. </summary>
    /// <param name="host"> The canonical host. </param>
    /// <param name="targetRoot"> The canonical absolute bundle target root. </param>
    internal SkillResolvedInstallTarget (
        SkillHostKind host,
        string targetRoot)
    {
        if (!ContractLiteralCodec.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        if (!Path.IsPathFullyQualified(targetRoot))
        {
            throw new ArgumentException("Target root must be an absolute path.", nameof(targetRoot));
        }

        Host = host;
        TargetRoot = Path.GetFullPath(targetRoot);
    }

    /// <summary> Gets the canonical host. </summary>
    public SkillHostKind Host { get; }

    /// <summary> Gets the canonical absolute bundle target root. </summary>
    public string TargetRoot { get; }
}

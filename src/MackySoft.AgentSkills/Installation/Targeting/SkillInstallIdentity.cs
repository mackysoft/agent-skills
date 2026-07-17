using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Names;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Installation.Targeting;

/// <summary> Identifies one installed SKILL instance. </summary>
public sealed class SkillInstallIdentity
{
    /// <summary> Initializes one installed SKILL identity. </summary>
    /// <param name="host"> The host. </param>
    /// <param name="scope"> The install scope. </param>
    /// <param name="targetRoot"> The canonical absolute host target root. </param>
    /// <param name="skillName"> The skill name. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="host" /> or <paramref name="scope" /> is unsupported. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="targetRoot" /> is not absolute. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="skillName" /> is <see langword="null" />. </exception>
    public SkillInstallIdentity (
        SkillHostKind host,
        SkillScopeKind scope,
        string targetRoot,
        SkillName skillName)
    {
        if (!ContractLiteralCodec.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        if (!ContractLiteralCodec.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported SKILL install scope.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        if (!Path.IsPathFullyQualified(targetRoot))
        {
            throw new ArgumentException("Target root must be an absolute path.", nameof(targetRoot));
        }

        ArgumentNullException.ThrowIfNull(skillName);

        Host = host;
        Scope = scope;
        TargetRoot = Path.GetFullPath(targetRoot);
        SkillName = skillName;
    }

    /// <summary> Gets the host. </summary>
    public SkillHostKind Host { get; }

    /// <summary> Gets the install scope. </summary>
    public SkillScopeKind Scope { get; }

    /// <summary> Gets the canonical absolute host target root. </summary>
    public string TargetRoot { get; }

    /// <summary> Gets the skill name. </summary>
    public SkillName SkillName { get; }
}

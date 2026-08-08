using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Installation.Targeting;

/// <summary> Identifies one installed SKILL instance. </summary>
public sealed class SkillInstallIdentity
{
    /// <summary> Initializes one installed SKILL identity. </summary>
    /// <param name="host"> The host. </param>
    /// <param name="scope"> The install scope. </param>
    /// <param name="targetRoot"> The canonical absolute bundle target root. </param>
    /// <param name="skillName"> The skill name. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="host" /> or <paramref name="scope" /> is unsupported. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="targetRoot" /> or <paramref name="skillName" /> is <see langword="null" />. </exception>
    public SkillInstallIdentity (
        HostKind host,
        SkillScopeKind scope,
        AbsolutePath targetRoot,
        SkillName skillName)
    {
        if (!Vocabulary.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        if (!Vocabulary.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported SKILL install scope.");
        }

        ArgumentNullException.ThrowIfNull(skillName);

        Host = host;
        Scope = scope;
        TargetRoot = targetRoot ?? throw new ArgumentNullException(nameof(targetRoot));
        SkillName = skillName;
    }

    /// <summary> Gets the host. </summary>
    public HostKind Host { get; }

    /// <summary> Gets the install scope. </summary>
    public SkillScopeKind Scope { get; }

    /// <summary> Gets the canonical absolute bundle target root. </summary>
    public AbsolutePath TargetRoot { get; }

    /// <summary> Gets the skill name. </summary>
    public SkillName SkillName { get; }
}

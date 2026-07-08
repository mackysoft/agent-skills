using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Installation.Targeting;

/// <summary> Defines supported SKILL install scopes. </summary>
public enum SkillScopeKind
{
    /// <summary> Project-local installation under a repository root. </summary>
    [ContractLiteral("project")]
    Project = 0,

    /// <summary> User-local installation under the target host's personal SKILL root. </summary>
    [ContractLiteral("user")]
    User = 1,
}

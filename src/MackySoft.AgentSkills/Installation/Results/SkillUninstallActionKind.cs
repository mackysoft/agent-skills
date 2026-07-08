using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Defines the outcome for one SKILL uninstall. </summary>
public enum SkillUninstallActionKind
{
    /// <summary> The managed target skill directory is planned to be deleted or was deleted. </summary>
    [ContractLiteral("deleted")]
    Deleted = 0,

    /// <summary> The target skill directory was already absent. </summary>
    [ContractLiteral("noOp")]
    NoOp = 1,

    /// <summary> The target skill directory exists but is not managed by Agent Skills. </summary>
    [ContractLiteral("skippedUnmanaged")]
    SkippedUnmanaged = 2,

    /// <summary> The target contains local modifications and force was not enabled. </summary>
    [ContractLiteral("blockedLocalModification")]
    BlockedLocalModification = 3,
}

using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Defines the outcome for one installed SKILL. </summary>
public enum SkillInstallActionKind
{
    /// <summary> The target skill directory is planned to be created or was created. </summary>
    [ContractLiteral("created")]
    Created = 0,

    /// <summary> The managed target skill directory is planned to be replaced or was replaced with the current canonical package. </summary>
    [ContractLiteral("updated")]
    Updated = 1,

    /// <summary> The target skill directory already contained matching content for the same host. </summary>
    [ContractLiteral("noOp")]
    NoOp = 2,

    /// <summary> The target contains managed non-current content and force was not enabled. </summary>
    [ContractLiteral("blockedManagedOverwrite")]
    BlockedManagedOverwrite = 3,

    /// <summary> The target contains local modifications and force was not enabled. </summary>
    [ContractLiteral("blockedLocalModification")]
    BlockedLocalModification = 4,

    /// <summary> The target is unmanaged and cannot be overwritten. </summary>
    [ContractLiteral("blockedUnmanaged")]
    BlockedUnmanaged = 5,
}

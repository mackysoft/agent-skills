using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Defines the outcome for one SKILL update. </summary>
public enum SkillUpdateActionKind
{
    /// <summary> The target skill directory is planned to be created or was created because it was missing. </summary>
    [ContractLiteral("created")]
    Created = 0,

    /// <summary> The target skill directory is planned to be replaced or was replaced with the current canonical package. </summary>
    [ContractLiteral("updated")]
    Updated = 1,

    /// <summary> The target skill directory already contained current content for the same host. </summary>
    [ContractLiteral("noOp")]
    NoOp = 2,

    /// <summary> The target contains local modifications and force was not enabled. </summary>
    [ContractLiteral("blockedLocalModification")]
    BlockedLocalModification = 3,

    /// <summary> The target is unmanaged and cannot be overwritten. </summary>
    [ContractLiteral("blockedUnmanaged")]
    BlockedUnmanaged = 4,

    /// <summary> The target was generated from a newer SKILL bundle and cannot be overwritten without force. </summary>
    [ContractLiteral("blockedVersionAhead")]
    BlockedVersionAhead = 5,
}

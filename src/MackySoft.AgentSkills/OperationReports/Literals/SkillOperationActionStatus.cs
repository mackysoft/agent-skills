using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.OperationReports.Literals;

/// <summary> Defines the coarse status for one operation action. </summary>
public enum SkillOperationActionStatus
{
    /// <summary> The action creates, updates, replaces, or deletes target files. </summary>
    [ContractLiteral("changed")]
    Changed = 0,

    /// <summary> The target already satisfies the requested operation. </summary>
    [ContractLiteral("noOp")]
    NoOp = 1,

    /// <summary> The action intentionally leaves an unmanaged target unchanged. </summary>
    [ContractLiteral("skipped")]
    Skipped = 2,

    /// <summary> The action is blocked and requires a different caller decision before it can change files. </summary>
    [ContractLiteral("blocked")]
    Blocked = 3,
}

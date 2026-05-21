namespace MackySoft.AgentSkills.OperationReports.Literals;

/// <summary> Defines the coarse status for one operation action. </summary>
internal enum SkillOperationActionStatus
{
    /// <summary> The action creates, updates, replaces, or deletes target files. </summary>
    Changed = 0,

    /// <summary> The target already satisfies the requested operation. </summary>
    NoOp = 1,

    /// <summary> The action intentionally leaves an unmanaged target unchanged. </summary>
    Skipped = 2,

    /// <summary> The action is blocked and requires a different caller decision before it can change files. </summary>
    Blocked = 3,
}

using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Defines structured SKILL diff change kinds. </summary>
public enum SkillDiffChangeKind
{
    /// <summary> A file is added. </summary>
    [ContractLiteral("added")]
    Added,

    /// <summary> A file is modified. </summary>
    [ContractLiteral("modified")]
    Modified,

    /// <summary> A file is deleted. </summary>
    [ContractLiteral("deleted")]
    Deleted,
}

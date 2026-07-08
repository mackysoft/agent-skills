using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Distribution;

/// <summary> Defines supported SKILL export output formats. </summary>
public enum SkillExportFormat
{
    /// <summary> Export materialized SKILL directories under the output root. </summary>
    [ContractLiteral("directory")]
    Directory = 0,

    /// <summary> Export materialized SKILL directories into one deterministic zip file. </summary>
    [ContractLiteral("zip")]
    Zip = 1,
}

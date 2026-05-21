namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents a target state with stable literals suitable for product CLI payloads. </summary>
/// <param name="Kind"> The stable target state literal. </param>
/// <param name="Code"> The failure code value represented by this state, when present. </param>
/// <param name="Message"> The failure message represented by this state, when present. </param>
/// <param name="FileSet"> The structured file-set drift details, when this state represents file-set drift. </param>
public sealed record SkillTargetStateReport (
    string Kind,
    string? Code = null,
    string? Message = null,
    SkillTargetFileSetReport? FileSet = null);

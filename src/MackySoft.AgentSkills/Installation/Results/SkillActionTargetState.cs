using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents the analyzed target state projected onto an operation action. </summary>
/// <param name="Kind"> The stable target state kind name. </param>
/// <param name="Code"> The failure code represented by this state, when present. </param>
/// <param name="Message"> The failure message represented by this state, when present. </param>
/// <param name="FileSet"> The structured file-set drift details, when this state represents file-set drift. </param>
public sealed record SkillActionTargetState (
    string Kind,
    SkillFailureCode? Code = null,
    string? Message = null,
    SkillActionTargetFileSet? FileSet = null);

using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents the analyzed target state projected onto an operation action. </summary>
/// <param name="Kind"> The analyzed target state kind. </param>
/// <param name="Code"> The failure code represented by this state, when present. </param>
/// <param name="Message"> The failure message represented by this state, when present. </param>
/// <param name="FileSet"> The structured file-set drift details, when this state represents file-set drift. </param>
/// <param name="InstalledSkillBundleVersion"> The installed target SKILL bundle version, when available. </param>
/// <param name="BundledSkillBundleVersion"> The bundled canonical package SKILL bundle version, when available. </param>
public sealed record SkillActionTargetState (
    SkillActionTargetStateKind Kind,
    SkillFailureCode? Code = null,
    string? Message = null,
    SkillActionTargetFileSet? FileSet = null,
    int? InstalledSkillBundleVersion = null,
    int? BundledSkillBundleVersion = null);

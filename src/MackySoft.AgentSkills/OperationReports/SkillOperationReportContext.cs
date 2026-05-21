using MackySoft.AgentSkills.Installation.Targeting;

namespace MackySoft.AgentSkills.OperationReports;

/// <summary> Provides operation context that is owned by the caller rather than stored on every result model. </summary>
/// <param name="Host"> The canonical host key used for the operation. </param>
/// <param name="Scope"> The install scope used for the operation. </param>
public sealed record SkillOperationReportContext (
    string Host,
    SkillScopeKind Scope);

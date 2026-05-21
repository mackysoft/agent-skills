namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral doctor result data. </summary>
/// <param name="Host"> The canonical host key diagnosed by the doctor workflow. </param>
/// <param name="Scope"> The stable install scope literal supplied by the caller. </param>
/// <param name="TargetRoot"> The canonical absolute target root diagnosed by the doctor workflow. </param>
/// <param name="IsHealthy"> Whether no error diagnostics were reported. </param>
/// <param name="Diagnostics"> The diagnostic reports in deterministic order. </param>
public sealed record SkillDoctorReport (
    string Host,
    string Scope,
    string TargetRoot,
    bool IsHealthy,
    IReadOnlyList<SkillDoctorDiagnosticReport> Diagnostics);

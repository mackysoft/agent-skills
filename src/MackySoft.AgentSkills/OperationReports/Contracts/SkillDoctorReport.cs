namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents product-neutral doctor result data. </summary>
/// <param name="Host"> The canonical host key diagnosed by the doctor workflow. </param>
/// <param name="Tiers"> The selected product-owned SKILL tier literals, or an empty list when the caller did not provide any. </param>
/// <param name="Scope"> The stable install scope literal supplied by the caller. </param>
/// <param name="TargetRoot"> The canonical absolute target root diagnosed by the doctor workflow. </param>
/// <param name="IsHealthy"> Whether no error diagnostics were reported. </param>
/// <param name="Diagnostics"> The diagnostic reports in deterministic order. </param>
public sealed record SkillDoctorReport (
    string Host,
    IReadOnlyList<string> Tiers,
    string Scope,
    string TargetRoot,
    bool IsHealthy,
    IReadOnlyList<SkillDoctorDiagnosticReport> Diagnostics);

namespace MackySoft.AgentSkills.OperationReports;

/// <summary> Represents one doctor diagnostic with stable severity and target state literals. </summary>
/// <param name="Severity"> The stable diagnostic severity literal. </param>
/// <param name="Code"> The machine-readable diagnostic code. </param>
/// <param name="Message"> The diagnostic message emitted by AgentSkills. </param>
/// <param name="SkillName"> The related skill name, or <see langword="null" /> for target-level diagnostics. </param>
/// <param name="TargetState"> The stable target state literal resolved from <paramref name="Code" />, when the code has managed drift meaning. </param>
public sealed record SkillDoctorDiagnosticReport (
    string Severity,
    string Code,
    string Message,
    string? SkillName,
    string? TargetState);

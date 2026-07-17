using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents one doctor diagnostic and its related target state. </summary>
public sealed class SkillDoctorDiagnosticReport
{
    internal SkillDoctorDiagnosticReport (
        SkillDoctorSeverity severity,
        string code,
        string message,
        string? skillName,
        SkillTargetStateKind? targetState)
    {
        if (!ContractLiteralCodec.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unsupported doctor diagnostic severity.");
        }

        if (targetState.HasValue && !ContractLiteralCodec.IsDefined(targetState.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(targetState), targetState, "Unsupported target state kind.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        OperationReportContractGuard.ValidateOptionalText(skillName, nameof(skillName));

        Severity = severity;
        Code = code;
        Message = message;
        SkillName = skillName;
        TargetState = targetState;
    }

    /// <summary> Gets the diagnostic severity. </summary>
    public SkillDoctorSeverity Severity { get; }

    /// <summary> Gets the machine-readable diagnostic code. </summary>
    public string Code { get; }

    /// <summary> Gets the diagnostic message emitted by AgentSkills. </summary>
    public string Message { get; }

    /// <summary> Gets the related skill name, or <see langword="null" /> for target-level diagnostics. </summary>
    public string? SkillName { get; }

    /// <summary> Gets the target state kind when the diagnostic code has managed drift meaning. </summary>
    public SkillTargetStateKind? TargetState { get; }
}

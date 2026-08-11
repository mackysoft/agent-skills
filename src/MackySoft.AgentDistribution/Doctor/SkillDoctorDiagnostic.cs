using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Doctor;

/// <summary> Represents one SKILL doctor diagnostic. </summary>
public sealed class SkillDoctorDiagnostic
{
    private SkillDoctorDiagnostic (
        SkillDoctorSeverity severity,
        AgentDistributionFailureCode code,
        string message,
        SkillName? skillName)
    {
        Severity = severity;
        Code = code;
        Message = message;
        SkillName = skillName;
    }

    /// <summary> Gets the diagnostic severity. </summary>
    public SkillDoctorSeverity Severity { get; }

    /// <summary> Gets the diagnostic code. </summary>
    public AgentDistributionFailureCode Code { get; }

    /// <summary> Gets the diagnostic message. </summary>
    public string Message { get; }

    /// <summary> Gets the related skill name, or <see langword="null" /> for target-level diagnostics. </summary>
    public SkillName? SkillName { get; }

    /// <summary> Creates an error diagnostic. </summary>
    /// <param name="code"> The diagnostic code. </param>
    /// <param name="message"> The diagnostic message. </param>
    /// <param name="skillName"> The related skill name, or <see langword="null" /> for target-level diagnostics. </param>
    /// <returns> The error diagnostic. </returns>
    public static SkillDoctorDiagnostic Error (
        string code,
        string message,
        string? skillName = null)
    {
        return Create(
            SkillDoctorSeverity.Error,
            new AgentDistributionFailureCode(code),
            message,
            skillName is null ? null : new SkillName(skillName));
    }

    /// <summary> Creates an error diagnostic from a SKILL failure code. </summary>
    /// <param name="code"> The diagnostic code. </param>
    /// <param name="message"> The diagnostic message. </param>
    /// <param name="skillName"> The related skill name, or <see langword="null" /> for target-level diagnostics. </param>
    /// <returns> The error diagnostic. </returns>
    public static SkillDoctorDiagnostic Error (
        AgentDistributionFailureCode code,
        string message,
        SkillName? skillName = null)
    {
        return Create(SkillDoctorSeverity.Error, code, message, skillName);
    }

    /// <summary> Creates an informational diagnostic. </summary>
    /// <param name="code"> The diagnostic code. </param>
    /// <param name="message"> The diagnostic message. </param>
    /// <param name="skillName"> The related skill name, or <see langword="null" /> for target-level diagnostics. </param>
    /// <returns> The informational diagnostic. </returns>
    public static SkillDoctorDiagnostic Info (
        string code,
        string message,
        string? skillName = null)
    {
        return Create(
            SkillDoctorSeverity.Info,
            new AgentDistributionFailureCode(code),
            message,
            skillName is null ? null : new SkillName(skillName));
    }

    private static SkillDoctorDiagnostic Create (
        SkillDoctorSeverity severity,
        AgentDistributionFailureCode code,
        string message,
        SkillName? skillName)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new SkillDoctorDiagnostic(severity, code, message, skillName);
    }
}

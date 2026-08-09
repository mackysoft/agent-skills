using MackySoft.AgentDistribution.Doctor;
using MackySoft.AgentDistribution.OperationReports.Literals;

namespace MackySoft.AgentDistribution.OperationReports.Contracts;

/// <summary> Represents one custom-agent diagnostic in a product-neutral payload. </summary>
public sealed class AgentDoctorDiagnosticReport
{
    internal AgentDoctorDiagnosticReport (
        string agentName,
        AgentDiagnosticArea area,
        SkillDoctorSeverity severity,
        string code,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        if (!Vocabulary.IsDefined(area))
        {
            throw new ArgumentOutOfRangeException(nameof(area), area, "Unsupported agent diagnostic area.");
        }
        if (!Vocabulary.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unsupported doctor diagnostic severity.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        AgentName = agentName;
        Area = area;
        Severity = severity;
        Code = code;
        Message = message;
    }

    /// <summary> Gets the diagnosed agent name. </summary>
    public string AgentName { get; }

    /// <summary> Gets the diagnosed custom-agent contract area. </summary>
    public AgentDiagnosticArea Area { get; }

    /// <summary> Gets the diagnostic severity. </summary>
    public SkillDoctorSeverity Severity { get; }

    /// <summary> Gets the machine-readable diagnostic code. </summary>
    public string Code { get; }

    /// <summary> Gets the diagnostic message. </summary>
    public string Message { get; }
}

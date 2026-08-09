using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Doctor;

/// <summary> Represents one custom-agent doctor diagnostic. </summary>
public sealed class AgentDoctorDiagnostic
{
    /// <summary> Initializes one immutable diagnostic. </summary>
    internal AgentDoctorDiagnostic (AgentName agentName, AgentDoctorDiagnosticArea area, bool isError, SkillFailureCode code, string message)
    {
        AgentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
        Area = area;
        IsError = isError;
        Code = code ?? throw new ArgumentNullException(nameof(code));
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Message = message;
    }

    /// <summary> Gets the diagnosed agent. </summary>
    public AgentName AgentName { get; }

    /// <summary> Gets the diagnosed contract area. </summary>
    public AgentDoctorDiagnosticArea Area { get; }

    /// <summary> Gets whether this diagnostic reports an error. </summary>
    public bool IsError { get; }

    /// <summary> Gets the machine-readable diagnostic code. </summary>
    public SkillFailureCode Code { get; }

    /// <summary> Gets the diagnostic message. </summary>
    public string Message { get; }
}

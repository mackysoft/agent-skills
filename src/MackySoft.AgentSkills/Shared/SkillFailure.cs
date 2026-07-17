namespace MackySoft.AgentSkills.Shared;

/// <summary> Represents one machine-readable SKILL operation failure. </summary>
public sealed class SkillFailure
{
    private SkillFailure (
        SkillFailureCode code,
        string message)
    {
        Code = code;
        Message = message;
    }

    /// <summary> Gets the machine-readable failure code. </summary>
    public SkillFailureCode Code { get; }

    /// <summary> Gets the user-facing failure message. </summary>
    public string Message { get; }

    /// <summary> Creates one SKILL failure. </summary>
    /// <param name="code"> The failure code. </param>
    /// <param name="message"> The user-facing failure message. </param>
    /// <returns> The created failure. </returns>
    public static SkillFailure Create (
        SkillFailureCode code,
        string message)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new SkillFailure(code, message);
    }
}

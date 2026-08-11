namespace MackySoft.AgentDistribution.Shared;

/// <summary> Represents one machine-readable Agent Distribution operation failure. </summary>
public sealed class AgentDistributionFailure
{
    private AgentDistributionFailure (
        AgentDistributionFailureCode code,
        string message)
    {
        Code = code;
        Message = message;
    }

    /// <summary> Gets the machine-readable failure code. </summary>
    public AgentDistributionFailureCode Code { get; }

    /// <summary> Gets the user-facing failure message. </summary>
    public string Message { get; }

    /// <summary> Creates one Agent Distribution failure. </summary>
    /// <param name="code"> The failure code. </param>
    /// <param name="message"> The user-facing failure message. </param>
    /// <returns> The created failure. </returns>
    public static AgentDistributionFailure Create (
        AgentDistributionFailureCode code,
        string message)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new AgentDistributionFailure(code, message);
    }
}

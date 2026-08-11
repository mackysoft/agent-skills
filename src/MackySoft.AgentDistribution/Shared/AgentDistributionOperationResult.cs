namespace MackySoft.AgentDistribution.Shared;

/// <summary> Represents an Agent Distribution operation result. </summary>
/// <typeparam name="T"> The successful value type. </typeparam>
public sealed class AgentDistributionOperationResult<T>
{
    private AgentDistributionOperationResult (
        T? value,
        AgentDistributionFailure? failure)
    {
        Value = value;
        Failure = failure;
    }

    /// <summary> Gets the successful value, or <see langword="null" /> when failed. </summary>
    public T? Value { get; }

    /// <summary> Gets the failure, or <see langword="null" /> when succeeded. </summary>
    public AgentDistributionFailure? Failure { get; }

    /// <summary> Gets a value indicating whether this result succeeded. </summary>
    public bool IsSuccess => Failure is null;

    /// <summary> Creates a successful result. </summary>
    /// <param name="value"> The successful value. </param>
    /// <returns> The successful result. </returns>
    public static AgentDistributionOperationResult<T> Success (T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new AgentDistributionOperationResult<T>(value, null);
    }

    /// <summary> Creates a failed result. </summary>
    /// <param name="code"> The failure code. </param>
    /// <param name="message"> The user-facing failure message. </param>
    /// <returns> The failed result. </returns>
    public static AgentDistributionOperationResult<T> FailureResult (
        AgentDistributionFailureCode code,
        string message)
    {
        return new AgentDistributionOperationResult<T>(default, AgentDistributionFailure.Create(code, message));
    }
}

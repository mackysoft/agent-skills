namespace MackySoft.AgentSkills.Shared;

/// <summary> Represents a SKILL library operation result. </summary>
/// <typeparam name="T"> The successful value type. </typeparam>
public sealed class SkillOperationResult<T>
{
    private SkillOperationResult (
        T? value,
        SkillFailure? failure)
    {
        Value = value;
        Failure = failure;
    }

    /// <summary> Gets the successful value, or <see langword="null" /> when failed. </summary>
    public T? Value { get; }

    /// <summary> Gets the failure, or <see langword="null" /> when succeeded. </summary>
    public SkillFailure? Failure { get; }

    /// <summary> Gets a value indicating whether this result succeeded. </summary>
    public bool IsSuccess => Failure is null;

    /// <summary> Creates a successful result. </summary>
    /// <param name="value"> The successful value. </param>
    /// <returns> The successful result. </returns>
    public static SkillOperationResult<T> Success (T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new SkillOperationResult<T>(value, null);
    }

    /// <summary> Creates a failed result. </summary>
    /// <param name="code"> The failure code. </param>
    /// <param name="message"> The user-facing failure message. </param>
    /// <returns> The failed result. </returns>
    public static SkillOperationResult<T> FailureResult (
        SkillFailureCode code,
        string message)
    {
        return new SkillOperationResult<T>(default, SkillFailure.Create(code, message));
    }
}

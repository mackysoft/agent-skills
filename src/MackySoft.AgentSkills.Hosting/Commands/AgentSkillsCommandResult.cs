using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents one command runtime result before product-specific emission. </summary>
public sealed record AgentSkillsCommandResult
{
    private AgentSkillsCommandResult (
        string command,
        object? payload,
        SkillFailure? failure,
        int exitCode)
    {
        Command = command;
        Payload = payload;
        Failure = failure;
        ExitCode = exitCode;
    }

    /// <summary> Gets the stable command literal. </summary>
    public string Command { get; }

    /// <summary> Gets the product-neutral command payload, or <see langword="null" /> for failures without payload data. </summary>
    public object? Payload { get; }

    /// <summary> Gets the structured failure when the command could not produce a payload. </summary>
    public SkillFailure? Failure { get; }

    /// <summary> Gets the process exit code recommended by the standard command runtime. </summary>
    public int ExitCode { get; }

    /// <summary> Gets a value indicating whether the command produced a payload. </summary>
    public bool IsSuccess => Failure is null;

    /// <summary> Creates a successful command result. </summary>
    /// <param name="command"> The stable command literal. </param>
    /// <param name="payload"> The product-neutral payload. </param>
    /// <param name="exitCode"> The process exit code. </param>
    /// <returns> The successful command result. </returns>
    public static AgentSkillsCommandResult Success (
        string command,
        object payload,
        int exitCode = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(payload);

        return new AgentSkillsCommandResult(command, payload, null, exitCode);
    }

    /// <summary> Creates a failed command result. </summary>
    /// <param name="command"> The stable command literal. </param>
    /// <param name="failure"> The structured failure. </param>
    /// <returns> The failed command result. </returns>
    public static AgentSkillsCommandResult FailureResult (
        string command,
        SkillFailure failure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(failure);

        return new AgentSkillsCommandResult(command, null, failure, 1);
    }
}

using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Hosting.Commands;

/// <summary> Represents one command runtime result before product-specific emission. </summary>
/// <param name="Command"> The stable command literal. </param>
/// <param name="Payload"> The product-neutral command payload, or <see langword="null" /> for failures without payload data. </param>
/// <param name="Failure"> The structured failure when the command could not produce a payload. </param>
/// <param name="ExitCode"> The process exit code recommended by the standard command runtime. </param>
public sealed record AgentSkillsCommandResult (
    string Command,
    object? Payload,
    SkillFailure? Failure,
    int ExitCode)
{
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

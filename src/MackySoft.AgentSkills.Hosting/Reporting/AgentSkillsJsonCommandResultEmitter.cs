using System.Text.Json;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Configuration;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Hosting.Reporting;

/// <summary> Emits Agent Skills command results as JSON to standard output. </summary>
public sealed class AgentSkillsJsonCommandResultEmitter : IAgentSkillsCommandResultEmitter
{
    private readonly AgentSkillsCommandRuntimeOptions options;

    /// <summary> Initializes a new instance of the <see cref="AgentSkillsJsonCommandResultEmitter" /> class. </summary>
    /// <param name="options"> The validated command runtime options. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="options" /> is <see langword="null" />. </exception>
    public AgentSkillsJsonCommandResultEmitter (AgentSkillsCommandRuntimeOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async ValueTask<int> EmitAsync (
        AgentSkillsCommandResult result,
        AgentSkillsCommandOutputOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var envelope = result.IsSuccess
            ? AgentSkillsCommandEnvelope.Success(this.options.ProductName, result.Command, result.Payload!)
            : AgentSkillsCommandEnvelope.Failure(this.options.ProductName, result.Command, result.Failure!);
        var serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = options.Pretty,
        };
        string json = JsonSerializer.Serialize(envelope, serializerOptions);
        await Console.Out.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }

    private sealed record AgentSkillsCommandEnvelope (
        string Product,
        string Command,
        string Status,
        object? Payload,
        AgentSkillsCommandError? Error)
    {
        public static AgentSkillsCommandEnvelope Success (
            string product,
            string command,
            object payload)
        {
            return new AgentSkillsCommandEnvelope(product, command, "ok", payload, null);
        }

        public static AgentSkillsCommandEnvelope Failure (
            string product,
            string command,
            SkillFailure failure)
        {
            return new AgentSkillsCommandEnvelope(
                product,
                command,
                "error",
                null,
                new AgentSkillsCommandError(failure.Code.Value, failure.Message));
        }
    }

    private sealed record AgentSkillsCommandError (
        string Code,
        string Message);
}

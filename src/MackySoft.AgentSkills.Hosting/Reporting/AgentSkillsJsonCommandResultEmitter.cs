using System.Text.Json;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosting.Configuration;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Hosting.Reporting;

/// <summary> Emits Agent Skills command results as JSON to standard output. </summary>
public sealed class AgentSkillsJsonCommandResultEmitter : IAgentSkillsCommandResultEmitter
{
    private readonly AgentSkillsCommandRuntimeConfiguration configuration;

    /// <summary> Initializes a new instance of the <see cref="AgentSkillsJsonCommandResultEmitter" /> class. </summary>
    /// <param name="configuration"> The validated command runtime configuration. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="configuration" /> is <see langword="null" />. </exception>
    public AgentSkillsJsonCommandResultEmitter (AgentSkillsCommandRuntimeConfiguration configuration)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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
            ? AgentSkillsCommandEnvelope.Success(configuration.ProductName, result.Command, result.Payload!)
            : AgentSkillsCommandEnvelope.Failure(configuration.ProductName, result.Command, result.Failure!);
        var serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = options.Pretty,
        };
        serializerOptions.Converters.Add(new ContractLiteralJsonConverterFactory());
        string json = JsonSerializer.Serialize(envelope, serializerOptions);
        await Console.Out.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }

    private sealed class AgentSkillsCommandEnvelope
    {
        private AgentSkillsCommandEnvelope (
            string product,
            string command,
            string status,
            object? payload,
            AgentSkillsCommandError? error)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(product);
            ArgumentException.ThrowIfNullOrWhiteSpace(command);
            ArgumentException.ThrowIfNullOrWhiteSpace(status);
            if ((payload is null) == (error is null))
            {
                throw new ArgumentException("A command envelope must contain either a payload or an error.");
            }

            Product = product;
            Command = command;
            Status = status;
            Payload = payload;
            Error = error;
        }

        public string Product { get; }

        public string Command { get; }

        public string Status { get; }

        public object? Payload { get; }

        public AgentSkillsCommandError? Error { get; }

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

    private sealed class AgentSkillsCommandError
    {
        public AgentSkillsCommandError (
            string code,
            string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            Code = code;
            Message = message;
        }

        public string Code { get; }

        public string Message { get; }
    }
}

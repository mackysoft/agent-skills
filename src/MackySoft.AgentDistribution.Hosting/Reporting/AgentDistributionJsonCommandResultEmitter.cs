using System.Text.Json;
using MackySoft.AgentDistribution.Hosting.Commands;
using MackySoft.AgentDistribution.Hosting.Configuration;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Hosting.Reporting;

/// <summary> Emits Agent Distribution command results as JSON to standard output. </summary>
public sealed class AgentDistributionJsonCommandResultEmitter : IAgentDistributionCommandResultEmitter
{
    private readonly AgentDistributionCommandRuntimeConfiguration configuration;

    /// <summary> Initializes a new instance of the <see cref="AgentDistributionJsonCommandResultEmitter" /> class. </summary>
    /// <param name="configuration"> The validated command runtime configuration. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="configuration" /> is <see langword="null" />. </exception>
    public AgentDistributionJsonCommandResultEmitter (AgentDistributionCommandRuntimeConfiguration configuration)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc />
    public async ValueTask<int> EmitAsync (
        AgentDistributionCommandResult result,
        AgentDistributionCommandOutputOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var envelope = result.IsSuccess
            ? AgentDistributionCommandEnvelope.Success(configuration.ProductName, result.Command, result.Payload!)
            : AgentDistributionCommandEnvelope.Failure(configuration.ProductName, result.Command, result.Failure!);
        var serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = options.Pretty,
        };
        serializerOptions.Converters.Add(new VocabularyJsonConverterFactory());
        string json = JsonSerializer.Serialize(envelope, serializerOptions);
        await Console.Out.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        return result.ExitCode;
    }

    private sealed class AgentDistributionCommandEnvelope
    {
        private AgentDistributionCommandEnvelope (
            string product,
            string command,
            string status,
            object? payload,
            AgentDistributionCommandError? error)
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

        public AgentDistributionCommandError? Error { get; }

        public static AgentDistributionCommandEnvelope Success (
            string product,
            string command,
            object payload)
        {
            return new AgentDistributionCommandEnvelope(product, command, "ok", payload, null);
        }

        public static AgentDistributionCommandEnvelope Failure (
            string product,
            string command,
            AgentDistributionFailure failure)
        {
            return new AgentDistributionCommandEnvelope(
                product,
                command,
                "error",
                null,
                new AgentDistributionCommandError(failure.Code.Value, failure.Message));
        }
    }

    private sealed class AgentDistributionCommandError
    {
        public AgentDistributionCommandError (
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

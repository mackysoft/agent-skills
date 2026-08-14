using System.Text.Json;
using MackySoft.AgentDistribution.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Reads and validates canonical authored v4 <c>bundle.json</c> files. </summary>
public sealed class AgentDistributionBundleDefinitionReader
{
    private readonly AgentDistributionBundleJsonSerializer serializer;

    /// <summary> Initializes the reader. </summary>
    public AgentDistributionBundleDefinitionReader (AgentDistributionBundleJsonSerializer serializer)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <summary> Reads one authored v4 definition. </summary>
    public async ValueTask<AgentDistributionOperationResult<AgentDistributionBundleDefinition>> ReadAsync (AbsolutePath bundleRoot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundleRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var validatedRootResult = AuthoredSourcePathResolver.ValidateDirectoryRoot(bundleRoot, "Source bundle root");
        if (!validatedRootResult.IsSuccess)
        {
            return Failure(validatedRootResult.Failure!.Message);
        }

        var pathResult = AuthoredSourcePathResolver.ResolveRegularFile(validatedRootResult.Value!, RootRelativePath.Parse("bundle.json"), "Source bundle.json");
        if (!pathResult.IsSuccess)
        {
            return Failure(pathResult.Failure!.Message);
        }

        try
        {
            var text = await File.ReadAllTextAsync(pathResult.Value!.Value, cancellationToken).ConfigureAwait(false);
            var definition = serializer.DeserializeDefinition(text);
            return string.Equals(text, serializer.SerializeDefinition(definition), StringComparison.Ordinal)
                ? AgentDistributionOperationResult<AgentDistributionBundleDefinition>.Success(definition)
                : Failure("Source bundle.json is not canonical.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or KeyNotFoundException or FormatException)
        {
            return Failure("Source bundle.json is invalid.");
        }
    }

    private static AgentDistributionOperationResult<AgentDistributionBundleDefinition> Failure (string message) => AgentDistributionOperationResult<AgentDistributionBundleDefinition>.FailureResult(AgentDistributionFailureCodes.SourceInvalid, message);
}

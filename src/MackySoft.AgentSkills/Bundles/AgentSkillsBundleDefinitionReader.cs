using System.Text.Json;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.FileSystem;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Reads and validates canonical authored v2 <c>bundle.json</c> files. </summary>
public sealed class AgentSkillsBundleDefinitionReader
{
    private readonly AgentSkillsBundleJsonSerializer serializer;

    /// <summary> Initializes the reader. </summary>
    public AgentSkillsBundleDefinitionReader (AgentSkillsBundleJsonSerializer serializer)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <summary> Reads one authored v2 definition. </summary>
    public async ValueTask<SkillOperationResult<AgentSkillsBundleDefinition>> ReadAsync (string bundleRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var rootResult = SourcePathBoundary.ParseRoot(bundleRoot, "Source bundle root");
        if (!rootResult.IsSuccess)
        {
            return Failure(rootResult.Failure!.Message);
        }

        var validatedRootResult = SourcePathBoundary.ValidateDirectoryRoot(rootResult.Value!, "Source bundle root");
        if (!validatedRootResult.IsSuccess)
        {
            return Failure(validatedRootResult.Failure!.Message);
        }

        var pathResult = SourcePathBoundary.ResolveRegularFile(validatedRootResult.Value!, "bundle.json", "Source bundle.json");
        if (!pathResult.IsSuccess)
        {
            return Failure(pathResult.Failure!.Message);
        }

        try
        {
            var text = await File.ReadAllTextAsync(pathResult.Value!.Value, cancellationToken).ConfigureAwait(false);
            var definition = serializer.DeserializeDefinition(text);
            return string.Equals(text, serializer.SerializeDefinition(definition), StringComparison.Ordinal)
                ? SkillOperationResult<AgentSkillsBundleDefinition>.Success(definition)
                : Failure("Source bundle.json is not canonical.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or KeyNotFoundException or FormatException)
        {
            return Failure("Source bundle.json is invalid.");
        }
    }

    private static SkillOperationResult<AgentSkillsBundleDefinition> Failure (string message) => SkillOperationResult<AgentSkillsBundleDefinition>.FailureResult(SkillFailureCodes.SourceInvalid, message);
}

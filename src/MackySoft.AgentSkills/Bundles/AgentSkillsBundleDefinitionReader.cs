using System.Text.Json;
using MackySoft.AgentSkills.Paths;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

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
    public async ValueTask<SkillOperationResult<AgentSkillsBundleDefinition>> ReadAsync (AbsolutePath bundleRoot, CancellationToken cancellationToken)
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

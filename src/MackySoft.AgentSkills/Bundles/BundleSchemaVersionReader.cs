using System.Text.Json;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.FileSystem;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Reads only the explicit schema discriminator from an authored bundle descriptor. </summary>
public sealed class BundleSchemaVersionReader
{
    /// <summary> Reads the schema version from <c>bundle.json</c>. </summary>
    public async ValueTask<SkillOperationResult<int>> ReadAsync (string bundleRoot, CancellationToken cancellationToken)
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
            await using var stream = File.OpenRead(pathResult.Value!.Value);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return SkillOperationResult<int>.Success(document.RootElement.GetProperty("schemaVersion").GetInt32());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return Failure("Source bundle.json is invalid.");
        }
    }

    private static SkillOperationResult<int> Failure (string message)
    {
        return SkillOperationResult<int>.FailureResult(SkillFailureCodes.SourceInvalid, message);
    }
}

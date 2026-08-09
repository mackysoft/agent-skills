using System.Text.Json;
using MackySoft.AgentDistribution.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Reads only the explicit schema discriminator from an authored bundle descriptor. </summary>
public sealed class BundleSchemaVersionReader
{
    /// <summary> Reads the schema version from <c>bundle.json</c>. </summary>
    public async ValueTask<SkillOperationResult<int>> ReadAsync (AbsolutePath bundleRoot, CancellationToken cancellationToken)
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

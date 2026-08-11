using System.Text.Json;
using MackySoft.AgentDistribution.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Reads and validates the authored <c>bundle.json</c> at a source bundle root. </summary>
public sealed class SkillBundleDefinitionReader
{
    private readonly SkillBundleJsonSerializer serializer;

    /// <summary> Initializes a reader with the canonical JSON contract. </summary>
    /// <param name="serializer"> The canonical bundle JSON serializer. </param>
    public SkillBundleDefinitionReader (SkillBundleJsonSerializer serializer)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <summary> Reads the canonical authored descriptor at <c>&lt;bundleRoot&gt;/bundle.json</c>. </summary>
    /// <param name="bundleRoot"> The root containing <c>bundle.json</c> and <c>definitions</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through file access. </param>
    /// <returns> The authored definition, or a source-invalid failure when it is missing, unsafe, malformed, or non-canonical. </returns>
    public async ValueTask<AgentDistributionOperationResult<SkillBundleDefinition>> ReadAsync (
        AbsolutePath bundleRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundleRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var validatedRootResult = AuthoredSourcePathResolver.ValidateDirectoryRoot(bundleRoot, "SKILL bundle root");
        if (!validatedRootResult.IsSuccess)
        {
            return Failure(validatedRootResult.Failure!.Message);
        }

        var pathResult = AuthoredSourcePathResolver.ResolveRegularFile(
            validatedRootResult.Value!,
            RootRelativePath.Parse("bundle.json"),
            "Source bundle path");
        if (!pathResult.IsSuccess)
        {
            return Failure(pathResult.Failure!.Message);
        }

        var textResult = await SkillBundleFileReader.ReadUtf8WithoutByteOrderMarkAsync(
                pathResult.Value!,
                AgentDistributionFailureCodes.SourceInvalid,
                cancellationToken)
            .ConfigureAwait(false);
        if (!textResult.IsSuccess)
        {
            return Failure(textResult.Failure!.Message);
        }

        SkillBundleDefinition definition;
        try
        {
            definition = serializer.DeserializeDefinition(textResult.Value!);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException or KeyNotFoundException)
        {
            return Failure("Source bundle.json is invalid.");
        }

        if (!string.Equals(textResult.Value, serializer.SerializeDefinition(definition), StringComparison.Ordinal))
        {
            return Failure("Source bundle.json is not canonical.");
        }

        return AgentDistributionOperationResult<SkillBundleDefinition>.Success(definition);
    }

    private static AgentDistributionOperationResult<SkillBundleDefinition> Failure (string message)
    {
        return AgentDistributionOperationResult<SkillBundleDefinition>.FailureResult(AgentDistributionFailureCodes.SourceInvalid, message);
    }
}

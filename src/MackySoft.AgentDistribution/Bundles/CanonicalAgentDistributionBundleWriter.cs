using MackySoft.AgentDistribution.Agents.Packaging;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Serialization;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Bundles;

/// <summary> Writes a v3 mixed bundle into a replaceable generated directory. </summary>
internal sealed class CanonicalAgentDistributionBundleWriter
{
    private readonly CanonicalSkillPackageWriter skillWriter;
    private readonly CanonicalAgentPackageWriter agentWriter;
    private readonly AgentDistributionBundleJsonSerializer serializer;
    private readonly CanonicalAgentDistributionBundleReader bundleReader;

    /// <summary> Initializes the writer. </summary>
    internal CanonicalAgentDistributionBundleWriter (
        CanonicalSkillPackageWriter skillWriter,
        CanonicalAgentPackageWriter agentWriter,
        AgentDistributionBundleJsonSerializer serializer,
        CanonicalAgentDistributionBundleReader bundleReader)
    {
        this.skillWriter = skillWriter ?? throw new ArgumentNullException(nameof(skillWriter));
        this.agentWriter = agentWriter ?? throw new ArgumentNullException(nameof(agentWriter));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
    }

    /// <summary> Writes a complete generated directory. </summary>
    public async ValueTask<SkillOperationResult<AbsolutePath>> WriteAsync (CanonicalAgentDistributionBundle bundle, AbsolutePath outputRoot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(outputRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var outputRootResult = ResolveOutputRoot(outputRoot);
        if (!outputRootResult.IsSuccess)
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(outputRootResult.Failure!.Code, outputRootResult.Failure.Message);
        }

        var full = outputRootResult.Value!;
        if (!full.TryGetParent(out var parent))
        {
            throw new InvalidOperationException("Generated v3 bundle output root parent could not be resolved.");
        }

        Directory.CreateDirectory(parent.Value);
        var staging = ContainedPath.Create(parent, RootRelativePath.Parse($".{Path.GetFileName(full.Value)}.staging.{Guid.NewGuid():N}")).Target;
        var backup = ContainedPath.Create(parent, RootRelativePath.Parse($".{Path.GetFileName(full.Value)}.backup.{Guid.NewGuid():N}")).Target;
        var published = false;
        try
        {
            foreach (var skill in bundle.Skills)
            {
                var result = await skillWriter.WriteToStagingAsync(
                    skill,
                    ContainedPath.Create(staging, RootRelativePath.Parse("skills")).Target,
                    cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return SkillOperationResult<AbsolutePath>.FailureResult(result.Failure!.Code, result.Failure.Message);
                }
            }

            foreach (var agent in bundle.Agents)
            {
                var result = await agentWriter.WriteToStagingAsync(
                    agent,
                    ContainedPath.Create(staging, RootRelativePath.Parse("agents")).Target,
                    cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return SkillOperationResult<AbsolutePath>.FailureResult(result.Failure!.Code, result.Failure.Message);
                }
            }

            await CanonicalTextFilePublisher.PublishAsync(
                ContainedPath.Create(staging, RootRelativePath.Parse("bundle.json")).Target,
                serializer.SerializeDescriptor(bundle.Descriptor),
                cancellationToken).ConfigureAwait(false);
            var stagedBundleResult = await bundleReader.ReadAsync(staging, cancellationToken).ConfigureAwait(false);
            if (!stagedBundleResult.IsSuccess)
            {
                return SkillOperationResult<AbsolutePath>.FailureResult(stagedBundleResult.Failure!.Code, stagedBundleResult.Failure.Message);
            }

            cancellationToken.ThrowIfCancellationRequested();
            CanonicalSkillBundleDirectoryPublisher.Publish(staging, full, backup);
            published = true;
            TryDeleteDirectory(backup);
            return SkillOperationResult<AbsolutePath>.Success(full);
        }
        finally
        {
            if (!published)
            {
                TryDeleteDirectory(staging);
            }
        }
    }

    private static SkillOperationResult<AbsolutePath> ResolveOutputRoot (AbsolutePath outputRoot)
    {
        var outputName = Path.GetFileName(outputRoot.Value);
        if (!string.Equals(outputName, "generated", StringComparison.Ordinal)
            && !string.Equals(outputName, "skills", StringComparison.Ordinal))
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(SkillFailureCodes.PathUnsafe, $"Generated v3 bundle output root must be named 'generated' or 'skills': {outputRoot}");
        }

        if (!FileSystemEntryInspector.TryInspect(
                outputRoot,
                out var outputRootObservation,
                out _)
            || outputRootObservation.State is not FileSystemEntryState.Missing and not FileSystemEntryState.Directory)
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(SkillFailureCodes.PathUnsafe, $"Generated v3 bundle output root must be a regular directory: {outputRoot}");
        }

        return SkillOperationResult<AbsolutePath>.Success(outputRoot);
    }

    private static void TryDeleteDirectory (AbsolutePath path)
    {
        try
        {
            if (Directory.Exists(path.Value))
            {
                Directory.Delete(path.Value, recursive: true);
            }
        }
        catch (IOException)
        {
            // Cleanup after a committed or failed publication must not hide its authoritative result.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup after a committed or failed publication must not hide its authoritative result.
        }
    }
}

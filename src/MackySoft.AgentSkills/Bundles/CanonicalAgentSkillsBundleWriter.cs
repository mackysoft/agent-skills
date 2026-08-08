using MackySoft.AgentSkills.Agents.Packaging;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Bundles;

/// <summary> Writes a v2 mixed bundle into a replaceable generated directory. </summary>
internal sealed class CanonicalAgentSkillsBundleWriter
{
    private readonly CanonicalSkillPackageWriter skillWriter;
    private readonly CanonicalAgentPackageWriter agentWriter;
    private readonly AgentSkillsBundleJsonSerializer serializer;
    private readonly CanonicalAgentSkillsBundleReader bundleReader;

    /// <summary> Initializes the writer. </summary>
    internal CanonicalAgentSkillsBundleWriter (
        CanonicalSkillPackageWriter skillWriter,
        CanonicalAgentPackageWriter agentWriter,
        AgentSkillsBundleJsonSerializer serializer,
        CanonicalAgentSkillsBundleReader bundleReader)
    {
        this.skillWriter = skillWriter ?? throw new ArgumentNullException(nameof(skillWriter));
        this.agentWriter = agentWriter ?? throw new ArgumentNullException(nameof(agentWriter));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.bundleReader = bundleReader ?? throw new ArgumentNullException(nameof(bundleReader));
    }

    /// <summary> Writes a complete generated directory. </summary>
    public async ValueTask<SkillOperationResult<string>> WriteAsync (CanonicalAgentSkillsBundle bundle, string outputRoot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var outputRootResult = ResolveOutputRoot(outputRoot);
        if (!outputRootResult.IsSuccess)
        {
            return outputRootResult;
        }

        var full = outputRootResult.Value!;
        var parent = Path.GetDirectoryName(full) ?? throw new InvalidOperationException("Generated v2 bundle output root parent could not be resolved.");
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, $".{Path.GetFileName(full)}.staging.{Guid.NewGuid():N}");
        var backup = Path.Combine(parent, $".{Path.GetFileName(full)}.backup.{Guid.NewGuid():N}");
        var published = false;
        try
        {
            foreach (var skill in bundle.Skills)
            {
                var result = await skillWriter.WriteToStagingAsync(skill, Path.Combine(staging, "skills"), cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return SkillOperationResult<string>.FailureResult(result.Failure!.Code, result.Failure.Message);
                }
            }

            foreach (var agent in bundle.Agents)
            {
                var result = await agentWriter.WriteToStagingAsync(agent, Path.Combine(staging, "agents"), cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return SkillOperationResult<string>.FailureResult(result.Failure!.Code, result.Failure.Message);
                }
            }

            await SkillPackageFileWriter.WriteAllTextAtomicallyAsync(Path.Combine(staging, "bundle.json"), serializer.SerializeDescriptor(bundle.Descriptor), cancellationToken).ConfigureAwait(false);
            var stagedBundleResult = await bundleReader.ReadAsync(staging, cancellationToken).ConfigureAwait(false);
            if (!stagedBundleResult.IsSuccess)
            {
                return SkillOperationResult<string>.FailureResult(stagedBundleResult.Failure!.Code, stagedBundleResult.Failure.Message);
            }

            cancellationToken.ThrowIfCancellationRequested();
            CanonicalSkillBundleDirectoryPublisher.Publish(staging, full, backup);
            published = true;
            TryDeleteDirectory(backup);
            return SkillOperationResult<string>.Success(full);
        }
        finally
        {
            if (!published)
            {
                TryDeleteDirectory(staging);
            }
        }
    }

    private static SkillOperationResult<string> ResolveOutputRoot (string outputRoot)
    {
        var fullOutputRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputRoot));
        var outputName = Path.GetFileName(fullOutputRoot);
        if (!string.Equals(outputName, "generated", StringComparison.Ordinal)
            && !string.Equals(outputName, "skills", StringComparison.Ordinal))
        {
            return SkillOperationResult<string>.FailureResult(SkillFailureCodes.PathUnsafe, $"Generated v2 bundle output root must be named 'generated' or 'skills': {fullOutputRoot}");
        }

        if (File.Exists(fullOutputRoot)
            || (Directory.Exists(fullOutputRoot) && !SkillPackageFileSystemEntryGuard.IsDirectory(fullOutputRoot)))
        {
            return SkillOperationResult<string>.FailureResult(SkillFailureCodes.PathUnsafe, $"Generated v2 bundle output root must be a regular directory: {fullOutputRoot}");
        }

        return SkillOperationResult<string>.Success(fullOutputRoot);
    }

    private static void TryDeleteDirectory (string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
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

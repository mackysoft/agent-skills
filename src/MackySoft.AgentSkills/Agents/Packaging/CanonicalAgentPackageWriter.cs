using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Packaging;

/// <summary> Writes one canonical agent package under an agents staging directory. </summary>
internal sealed class CanonicalAgentPackageWriter
{
    /// <summary> Writes one agent package. </summary>
    public async ValueTask<SkillOperationResult<string>> WriteToStagingAsync (CanonicalAgentPackage package, string agentsStagingRoot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentsStagingRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(agentsStagingRoot));
        Directory.CreateDirectory(root);
        var directoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(root, package.Manifest.AgentName.Value);
        if (!directoryResult.IsSuccess)
        {
            return SkillOperationResult<string>.FailureResult(directoryResult.Failure!.Code, directoryResult.Failure.Message);
        }

        var directory = directoryResult.Value!;
        foreach (var file in package.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pathResult = SkillPackagePathBoundary.ResolvePackageFilePathUnderRoot(root, directory, file.RelativePath);
            if (!pathResult.IsSuccess)
            {
                return SkillOperationResult<string>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
            }

            await SkillPackageFileWriter.WriteAllTextAtomicallyAsync(pathResult.Value!, file.Content, cancellationToken).ConfigureAwait(false);
        }

        return SkillOperationResult<string>.Success(directory);
    }
}

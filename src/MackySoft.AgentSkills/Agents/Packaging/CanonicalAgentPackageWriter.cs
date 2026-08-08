using MackySoft.AgentSkills.Packaging.Paths;
using MackySoft.AgentSkills.Serialization;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Agents.Packaging;

/// <summary> Writes one canonical agent package under an agents staging directory. </summary>
internal sealed class CanonicalAgentPackageWriter
{
    /// <summary> Writes one agent package. </summary>
    public async ValueTask<SkillOperationResult<AbsolutePath>> WriteToStagingAsync (CanonicalAgentPackage package, AbsolutePath agentsStagingRoot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(agentsStagingRoot);
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(agentsStagingRoot.Value);
        var directoryResult = PackagePathResolver.ResolveUnderRoot(
            agentsStagingRoot,
            ContainedPath.Create(agentsStagingRoot, RootRelativePath.Parse(package.Manifest.AgentName.Value)).Target);
        if (!directoryResult.IsSuccess)
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(directoryResult.Failure!.Code, directoryResult.Failure.Message);
        }

        var directory = directoryResult.Value!;
        foreach (var file in package.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pathResult = PackagePathResolver.ResolveUnderRoot(
                agentsStagingRoot,
                ContainedPath.Create(directory, file.RelativePath.RootRelativePath).Target);
            if (!pathResult.IsSuccess)
            {
                return SkillOperationResult<AbsolutePath>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
            }

            await CanonicalTextFilePublisher.PublishAsync(pathResult.Value!, file.Content, cancellationToken).ConfigureAwait(false);
        }

        return SkillOperationResult<AbsolutePath>.Success(directory);
    }
}

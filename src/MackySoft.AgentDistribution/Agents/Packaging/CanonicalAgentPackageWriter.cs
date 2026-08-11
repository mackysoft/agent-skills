using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Serialization;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Packaging;

/// <summary> Writes one canonical agent package under an agents staging directory. </summary>
internal sealed class CanonicalAgentPackageWriter
{
    /// <summary> Writes one agent package. </summary>
    public async ValueTask<AgentDistributionOperationResult<AbsolutePath>> WriteToStagingAsync (CanonicalAgentPackage package, AbsolutePath agentsStagingRoot, CancellationToken cancellationToken)
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
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(directoryResult.Failure!.Code, directoryResult.Failure.Message);
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
                return AgentDistributionOperationResult<AbsolutePath>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
            }

            await CanonicalTextFilePublisher.PublishAsync(pathResult.Value!, file.Content, cancellationToken).ConfigureAwait(false);
        }

        return AgentDistributionOperationResult<AbsolutePath>.Success(directory);
    }
}

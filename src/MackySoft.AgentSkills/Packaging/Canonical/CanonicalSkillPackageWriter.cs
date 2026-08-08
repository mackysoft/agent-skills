using MackySoft.AgentSkills.Packaging.Paths;
using MackySoft.AgentSkills.Serialization;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Packaging.Canonical;

/// <summary> Writes one canonical SKILL package into a bundle staging directory. </summary>
public sealed class CanonicalSkillPackageWriter
{
    /// <summary> Writes one package into its skill-name directory under the staging root. </summary>
    /// <param name="package"> The canonical package to stage. </param>
    /// <param name="stagingRoot"> The bundle staging directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The full staged package directory path or failure. </returns>
    internal async ValueTask<SkillOperationResult<AbsolutePath>> WriteToStagingAsync (
        CanonicalSkillPackage package,
        AbsolutePath stagingRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(stagingRoot);
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(stagingRoot.Value);
        var skillDirectoryResult = PackagePathResolver.ResolveUnderRoot(
            stagingRoot,
            ContainedPath.Create(stagingRoot, RootRelativePath.Parse(package.Manifest.SkillName.Value)).Target);
        if (!skillDirectoryResult.IsSuccess)
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(
                skillDirectoryResult.Failure!.Code,
                skillDirectoryResult.Failure.Message);
        }

        var skillDirectory = skillDirectoryResult.Value!;
        foreach (var file in package.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePathResult = PackagePathResolver.ResolveUnderRoot(
                stagingRoot,
                ContainedPath.Create(skillDirectory, file.RelativePath.RootRelativePath).Target);
            if (!filePathResult.IsSuccess)
            {
                return SkillOperationResult<AbsolutePath>.FailureResult(
                    filePathResult.Failure!.Code,
                    filePathResult.Failure.Message);
            }

            await CanonicalTextFilePublisher.PublishAsync(
                    filePathResult.Value!,
                    file.Content,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return SkillOperationResult<AbsolutePath>.Success(skillDirectory);
    }
}

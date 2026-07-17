using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Packaging.Canonical;

/// <summary> Writes one canonical SKILL package into a bundle staging directory. </summary>
public sealed class CanonicalSkillPackageWriter
{
    /// <summary> Writes one package into its skill-name directory under the staging root. </summary>
    /// <param name="package"> The canonical package to stage. </param>
    /// <param name="stagingRoot"> The bundle staging directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The full staged package directory path or failure. </returns>
    internal async ValueTask<SkillOperationResult<string>> WriteToStagingAsync (
        CanonicalSkillPackage package,
        string stagingRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var fullStagingRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagingRoot));
        Directory.CreateDirectory(fullStagingRoot);
        var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(
            fullStagingRoot,
            package.Manifest.SkillName.Value);
        if (!skillDirectoryResult.IsSuccess)
        {
            return SkillOperationResult<string>.FailureResult(
                skillDirectoryResult.Failure!.Code,
                skillDirectoryResult.Failure.Message);
        }

        var skillDirectory = skillDirectoryResult.Value!;
        foreach (var file in package.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePathResult = SkillPackagePathBoundary.ResolvePackageFilePathUnderRoot(
                fullStagingRoot,
                skillDirectory,
                file.RelativePath);
            if (!filePathResult.IsSuccess)
            {
                return SkillOperationResult<string>.FailureResult(
                    filePathResult.Failure!.Code,
                    filePathResult.Failure.Message);
            }

            await SkillPackageFileWriter.WriteAllTextAtomicallyAsync(
                    filePathResult.Value!,
                    file.Content,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return SkillOperationResult<string>.Success(skillDirectory);
    }
}

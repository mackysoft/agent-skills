using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Packaging.Canonical;

/// <summary> Writes generated canonical SKILL packages to an output directory. </summary>
public sealed class CanonicalSkillPackageWriter
{
    /// <summary> Writes all packages to the output root. </summary>
    /// <param name="packages"> The generated canonical packages. </param>
    /// <param name="outputRoot"> The generated package output directory. </param>
    /// <param name="cleanOutputRoot"> Whether to remove the existing output root before writing. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The full output root path or failure. </returns>
    public async ValueTask<SkillOperationResult<string>> WriteAllAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        string outputRoot,
        bool cleanOutputRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        cancellationToken.ThrowIfCancellationRequested();

        if (packages.Count == 0)
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                "Generated SKILL package set must not be empty.");
        }

        var fullOutputRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputRoot));
        if (cleanOutputRoot)
        {
            var cleanResult = CleanOutputRoot(fullOutputRoot);
            if (!cleanResult.IsSuccess)
            {
                return SkillOperationResult<string>.FailureResult(cleanResult.Failure!.Code, cleanResult.Failure.Message);
            }
        }

        Directory.CreateDirectory(fullOutputRoot);
        foreach (var package in packages.OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillDirectoryResult = SkillPackagePathBoundary.ResolvePackageDirectory(fullOutputRoot, package.Manifest.SkillName.Value);
            if (!skillDirectoryResult.IsSuccess)
            {
                return SkillOperationResult<string>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
            }

            var skillDirectory = skillDirectoryResult.Value!;
            foreach (var file in package.Files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal))
            {
                var filePathResult = SkillPackagePathBoundary.ResolvePackageFilePathUnderRoot(fullOutputRoot, skillDirectory, file.RelativePath);
                if (!filePathResult.IsSuccess)
                {
                    return SkillOperationResult<string>.FailureResult(filePathResult.Failure!.Code, filePathResult.Failure.Message);
                }

                await SkillPackageFileWriter.WriteAllTextAtomicallyAsync(filePathResult.Value!, file.Content, cancellationToken).ConfigureAwait(false);
            }
        }

        return SkillOperationResult<string>.Success(fullOutputRoot);
    }

    private static SkillOperationResult<bool> CleanOutputRoot (string outputRoot)
    {
        var outputDirectoryName = Path.GetFileName(outputRoot);
        if (!string.Equals(outputDirectoryName, "generated", StringComparison.Ordinal)
            && !string.Equals(outputDirectoryName, "skills", StringComparison.Ordinal))
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Generated SKILL output root must be named 'generated' or 'skills': {outputRoot}");
        }

        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }

        return SkillOperationResult<bool>.Success(true);
    }
}

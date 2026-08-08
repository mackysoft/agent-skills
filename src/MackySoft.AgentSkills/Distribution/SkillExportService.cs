using MackySoft.AgentSkills.Materialization;
using MackySoft.AgentSkills.Packaging.Canonical;
using MackySoft.AgentSkills.Packaging.Paths;
using MackySoft.AgentSkills.Serialization;
using MackySoft.AgentSkills.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentSkills.Distribution;

/// <summary> Exports SKILL packages to a host-materialized output directory. </summary>
public sealed class SkillExportService
{
    private readonly SkillMaterializationService materializationService;

    /// <summary> Initializes a new instance of the <see cref="SkillExportService" /> class. </summary>
    /// <param name="materializationService"> The materialization service. </param>
    public SkillExportService (SkillMaterializationService materializationService)
    {
        this.materializationService = materializationService ?? throw new ArgumentNullException(nameof(materializationService));
    }

    /// <summary> Exports all packages into an output root. </summary>
    /// <param name="packages"> The canonical packages. </param>
    /// <param name="host"> The target host. </param>
    /// <param name="outputRoot"> The output root directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The output root or failure. </returns>
    public ValueTask<SkillOperationResult<AbsolutePath>> ExportAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        HostKind host,
        AbsolutePath outputRoot,
        CancellationToken cancellationToken = default)
    {
        return ExportAsync(packages, host, outputRoot, SkillExportFormat.Directory, cancellationToken);
    }

    /// <summary> Exports all packages into an output path. </summary>
    /// <param name="packages"> The canonical packages. </param>
    /// <param name="host"> The target host. </param>
    /// <param name="outputRoot"> The output directory or zip file path. </param>
    /// <param name="format"> The output format. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The output path or failure. </returns>
    public async ValueTask<SkillOperationResult<AbsolutePath>> ExportAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        HostKind host,
        AbsolutePath outputRoot,
        SkillExportFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(outputRoot);
        cancellationToken.ThrowIfCancellationRequested();

        return format switch
        {
            SkillExportFormat.Directory => await ExportDirectoryAsync(packages, host, outputRoot, cancellationToken).ConfigureAwait(false),
            SkillExportFormat.Zip => await ExportZipAsync(packages, host, outputRoot, cancellationToken).ConfigureAwait(false),
            _ => SkillOperationResult<AbsolutePath>.FailureResult(
                SkillFailureCodes.InputInvalid,
                $"Unsupported SKILL export format: {format}"),
        };
    }

    private async ValueTask<SkillOperationResult<AbsolutePath>> ExportDirectoryAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        HostKind host,
        AbsolutePath outputRoot,
        CancellationToken cancellationToken)
    {
        if (packages.Count == 0)
        {
            Directory.CreateDirectory(outputRoot.Value);
        }

        foreach (var package in packages.OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal))
        {
            var materializedResult = materializationService.Materialize(package, host);
            if (!materializedResult.IsSuccess)
            {
                return SkillOperationResult<AbsolutePath>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
            }

            var skillDirectoryResult = PackagePathResolver.ResolveUnderRoot(
                outputRoot,
                ContainedPath.Create(outputRoot, RootRelativePath.Parse(package.Manifest.SkillName.Value)).Target);
            if (!skillDirectoryResult.IsSuccess)
            {
                return SkillOperationResult<AbsolutePath>.FailureResult(skillDirectoryResult.Failure!.Code, skillDirectoryResult.Failure.Message);
            }

            var skillDirectory = skillDirectoryResult.Value!;
            foreach (var file in materializedResult.Value!.Files)
            {
                var filePathResult = PackagePathResolver.ResolveUnderRoot(
                    outputRoot,
                    ContainedPath.Create(skillDirectory, file.RelativePath.RootRelativePath).Target);
                if (!filePathResult.IsSuccess)
                {
                    return SkillOperationResult<AbsolutePath>.FailureResult(filePathResult.Failure!.Code, filePathResult.Failure.Message);
                }

                await CanonicalTextFilePublisher.PublishAsync(filePathResult.Value!, file.Content, cancellationToken).ConfigureAwait(false);
            }
        }

        return SkillOperationResult<AbsolutePath>.Success(outputRoot);
    }

    private async ValueTask<SkillOperationResult<AbsolutePath>> ExportZipAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        HostKind host,
        AbsolutePath outputPath,
        CancellationToken cancellationToken)
    {
        if (!outputPath.TryGetParent(out _))
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"SKILL export zip output path is invalid: {outputPath}");
        }

        var zipEntries = new List<PackageTextFile>();
        foreach (var package in packages.OrderBy(static package => package.Manifest.SkillName.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var materializedResult = materializationService.Materialize(package, host);
            if (!materializedResult.IsSuccess)
            {
                return SkillOperationResult<AbsolutePath>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
            }

            foreach (var file in materializedResult.Value!.Files.OrderBy(static file => file.RelativePath.Value, StringComparer.Ordinal))
            {
                var entryPath = $"{package.Manifest.SkillName.Value}/{file.RelativePath.Value}";
                if (!PackageRelativePath.TryParse(entryPath, out var relativeEntryPath))
                {
                    return SkillOperationResult<AbsolutePath>.FailureResult(
                        SkillFailureCodes.PathUnsafe,
                        $"SKILL export zip entry path is unsafe: {entryPath}");
                }

                zipEntries.Add(new PackageTextFile(relativeEntryPath, file.Content));
            }
        }

        try
        {
            await DeterministicPackageArchivePublisher.PublishAsync(
                    outputPath,
                    zipEntries,
                    cancellationToken)
                .ConfigureAwait(false);
            return SkillOperationResult<AbsolutePath>.Success(outputPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return SkillOperationResult<AbsolutePath>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"Failed to export SKILL zip: {outputPath}. {ex.Message}");
        }
    }
}

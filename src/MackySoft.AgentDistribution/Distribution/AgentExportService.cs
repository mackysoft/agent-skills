using MackySoft.AgentDistribution.Agents.Distribution;
using MackySoft.AgentDistribution.Materialization;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Serialization;
using MackySoft.AgentDistribution.Shared;
using MackySoft.AgentDistribution.Skills.Bundles;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Distribution;

/// <summary> Exports selected custom agents and their resolved SKILL dependencies without changing installation state. </summary>
public sealed class AgentExportService
{
    private readonly SkillMaterializationService skillMaterializationService;

    /// <summary> Initializes an agent export service. </summary>
    /// <param name="skillMaterializationService"> The service that materializes resolved SKILL dependencies for the requested host. </param>
    public AgentExportService (SkillMaterializationService skillMaterializationService)
    {
        this.skillMaterializationService = skillMaterializationService ?? throw new ArgumentNullException(nameof(skillMaterializationService));
    }

    /// <summary> Exports one selected agent catalog to a directory or deterministic zip archive. </summary>
    /// <param name="catalog"> The selected agents and their resolved SKILL dependency closure. </param>
    /// <param name="hostId"> The host used for agent artifacts and SKILL materialization. </param>
    /// <param name="outputPath"> The output directory or zip file path. </param>
    /// <param name="format"> The output format. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through materialization and output writes. </param>
    /// <returns> The canonical output path, or a structured failure produced before publication. </returns>
    public async ValueTask<AgentDistributionOperationResult<AbsolutePath>> ExportAsync (
        AgentPackageCatalog catalog,
        HostKind hostId,
        AbsolutePath outputPath,
        PackageExportFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }
        ArgumentNullException.ThrowIfNull(outputPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Vocabulary.IsDefined(format))
        {
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                AgentDistributionFailureCodes.InputInvalid,
                $"Unsupported agent export format: {format}");
        }

        var planResult = CreateExportPlan(catalog, hostId, cancellationToken);
        if (!planResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                planResult.Failure!.Code,
                planResult.Failure.Message);
        }

        return format switch
        {
            PackageExportFormat.Directory => await ExportDirectoryAsync(planResult.Value!, outputPath, cancellationToken).ConfigureAwait(false),
            PackageExportFormat.Zip => await ExportZipAsync(planResult.Value!, outputPath, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Validated agent export format is unsupported: {format}"),
        };
    }

    private AgentDistributionOperationResult<IReadOnlyList<PackageTextFile>> CreateExportPlan (
        AgentPackageCatalog catalog,
        HostKind hostId,
        CancellationToken cancellationToken)
    {
        var entries = new List<PackageTextFile>();
        var entryPaths = new HashSet<PackageRelativePath>(PackageRelativePath.PortableFileSystemComparer);

        foreach (var agent in catalog.SelectedAgents.OrderBy(static agent => agent.Manifest.AgentName.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var artifacts = agent.Manifest.HostArtifacts
                .Where(artifact => artifact.HostId == hostId)
                .OrderBy(static artifact => artifact.Path.Value, StringComparer.Ordinal)
                .ToArray();
            if (artifacts.Length == 0)
            {
                return AgentDistributionOperationResult<IReadOnlyList<PackageTextFile>>.FailureResult(
                    AgentDistributionFailureCodes.HostUnsupported,
                    $"Agent '{agent.Manifest.AgentName.Value}' does not provide artifacts for host '{Vocabulary.GetText(hostId)}'.");
            }

            var fileByPath = agent.Files.ToDictionary(static file => file.RelativePath);
            foreach (var artifact in artifacts)
            {
                if (!fileByPath.TryGetValue(artifact.Path, out var artifactFile))
                {
                    return AgentDistributionOperationResult<IReadOnlyList<PackageTextFile>>.FailureResult(
                        AgentDistributionFailureCodes.ManifestInvalid,
                        $"Agent host artifact does not match its package for '{agent.Manifest.AgentName.Value}': {artifact.Path}");
                }

                var exportPath = PackageRelativePath.Parse($"agents/{artifact.HostTargetRelativePath.Value}");
                var addResult = AddEntry(entries, entryPaths, exportPath, artifactFile.Content);
                if (!addResult.IsSuccess)
                {
                    return addResult;
                }
            }
        }

        foreach (var skill in catalog.ResolvedSkills.OrderBy(static skill => skill.Manifest.SkillName.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var materializationResult = skillMaterializationService.Materialize(skill, hostId);
            if (!materializationResult.IsSuccess)
            {
                return AgentDistributionOperationResult<IReadOnlyList<PackageTextFile>>.FailureResult(
                    materializationResult.Failure!.Code,
                    materializationResult.Failure.Message);
            }

            foreach (var file in materializationResult.Value!.Files.OrderBy(static file => file.RelativePath.Value, StringComparer.Ordinal))
            {
                var exportPath = PackageRelativePath.Parse($"skills/{skill.Manifest.SkillName.Value}/{file.RelativePath.Value}");
                var addResult = AddEntry(entries, entryPaths, exportPath, file.Content);
                if (!addResult.IsSuccess)
                {
                    return addResult;
                }
            }
        }

        return AgentDistributionOperationResult<IReadOnlyList<PackageTextFile>>.Success(
            Array.AsReadOnly(entries.OrderBy(static entry => entry.RelativePath.Value, StringComparer.Ordinal).ToArray()));
    }

    private static AgentDistributionOperationResult<IReadOnlyList<PackageTextFile>> AddEntry (
        List<PackageTextFile> entries,
        HashSet<PackageRelativePath> entryPaths,
        PackageRelativePath relativePath,
        string content)
    {
        if (!entryPaths.Add(relativePath))
        {
            return AgentDistributionOperationResult<IReadOnlyList<PackageTextFile>>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"Agent export artifacts resolve to the same output path: {relativePath}");
        }

        entries.Add(new PackageTextFile(relativePath, content));
        return AgentDistributionOperationResult<IReadOnlyList<PackageTextFile>>.Success(entries);
    }

    private static async ValueTask<AgentDistributionOperationResult<AbsolutePath>> ExportDirectoryAsync (
        IReadOnlyList<PackageTextFile> entries,
        AbsolutePath outputPath,
        CancellationToken cancellationToken)
    {
        var outputResult = ResolveDirectoryOutputPath(outputPath);
        if (!outputResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(outputResult.Failure!.Code, outputResult.Failure.Message);
        }

        var fullOutputPath = outputResult.Value!;
        if (!fullOutputPath.TryGetParent(out var parentPath))
        {
            throw new InvalidOperationException("Agent export directory must have a parent directory.");
        }

        var operationId = Guid.NewGuid().ToString("N");
        var outputName = Path.GetFileName(fullOutputPath.Value);
        var stagingPath = ContainedPath.Create(parentPath, RootRelativePath.Parse($".{outputName}.staging.{operationId}")).Target;
        var backupPath = ContainedPath.Create(parentPath, RootRelativePath.Parse($".{outputName}.backup.{operationId}")).Target;
        var published = false;
        try
        {
            Directory.CreateDirectory(ContainedPath.Create(stagingPath, RootRelativePath.Parse("agents")).Target.Value);
            Directory.CreateDirectory(ContainedPath.Create(stagingPath, RootRelativePath.Parse("skills")).Target.Value);
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePathResult = PackagePathResolver.ResolveUnderRoot(
                    stagingPath,
                    ContainedPath.Create(stagingPath, entry.RelativePath.RootRelativePath).Target);
                if (!filePathResult.IsSuccess)
                {
                    return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                        filePathResult.Failure!.Code,
                        filePathResult.Failure.Message);
                }

                await CanonicalTextFilePublisher.PublishAsync(
                        filePathResult.Value!,
                        entry.Content,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            CanonicalSkillBundleDirectoryPublisher.Publish(stagingPath, fullOutputPath, backupPath);
            published = true;
            TryDeleteDirectory(backupPath);
            return AgentDistributionOperationResult<AbsolutePath>.Success(fullOutputPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetWriteFailed,
                $"Failed to export agents to directory '{fullOutputPath.Value}': {exception.Message}");
        }
        finally
        {
            if (!published)
            {
                TryDeleteDirectory(stagingPath);
            }
        }
    }

    private static async ValueTask<AgentDistributionOperationResult<AbsolutePath>> ExportZipAsync (
        IReadOnlyList<PackageTextFile> entries,
        AbsolutePath outputPath,
        CancellationToken cancellationToken)
    {
        var outputResult = ResolveZipOutputPath(outputPath);
        if (!outputResult.IsSuccess)
        {
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(outputResult.Failure!.Code, outputResult.Failure.Message);
        }

        var fullOutputPath = outputResult.Value!;
        try
        {
            await DeterministicPackageArchivePublisher.PublishAsync(
                    fullOutputPath,
                    entries,
                    cancellationToken)
                .ConfigureAwait(false);
            return AgentDistributionOperationResult<AbsolutePath>.Success(fullOutputPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                AgentDistributionFailureCodes.InstallTargetWriteFailed,
                $"Failed to export agents to zip '{fullOutputPath.Value}': {exception.Message}");
        }
    }

    private static AgentDistributionOperationResult<AbsolutePath> ResolveDirectoryOutputPath (AbsolutePath outputPath)
    {
        var fullOutputPath = outputPath;
        var hasParent = fullOutputPath.TryGetParent(out var parentPath);
        var inspected = FileSystemEntryInspector.TryInspect(
            fullOutputPath,
            out var observation,
            out _);
        if (!hasParent
            || !inspected
            || observation is null
            || observation.State is not FileSystemEntryState.Missing and not FileSystemEntryState.Directory)
        {
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"Agent export output path must be a regular directory or an unused path: {fullOutputPath}");
        }

        return PackagePathResolver.ResolveUnderRoot(parentPath!, fullOutputPath);
    }

    private static AgentDistributionOperationResult<AbsolutePath> ResolveZipOutputPath (AbsolutePath outputPath)
    {
        var fullOutputPath = outputPath;
        var hasParent = fullOutputPath.TryGetParent(out var parentPath);
        var inspected = FileSystemEntryInspector.TryInspect(
            fullOutputPath,
            out var observation,
            out _);
        if (!hasParent
            || !inspected
            || observation is null
            || observation.State is not FileSystemEntryState.Missing and not FileSystemEntryState.RegularFile)
        {
            return AgentDistributionOperationResult<AbsolutePath>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"Agent export zip output path must be a regular file or an unused path: {fullOutputPath}");
        }

        return PackagePathResolver.ResolveUnderRoot(parentPath!, fullOutputPath);
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
            // Cleanup must not replace the authoritative export result.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup must not replace the authoritative export result.
        }
    }

}

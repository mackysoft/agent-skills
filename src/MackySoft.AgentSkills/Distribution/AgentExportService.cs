using System.IO.Compression;
using System.Text;
using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Materialization;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Distribution;

/// <summary> Exports selected custom agents and their resolved SKILL dependencies without changing installation state. </summary>
public sealed class AgentExportService
{
    private static readonly DateTimeOffset ZipEntryTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

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
    public async ValueTask<SkillOperationResult<string>> ExportAsync (
        AgentPackageCatalog catalog,
        AgentHostKind hostId,
        string outputPath,
        SkillExportFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Vocabulary.IsDefined(format))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.InputInvalid,
                $"Unsupported agent export format: {format}");
        }

        var hostLiteral = Vocabulary.GetText(hostId);
        if (!Vocabulary.TryGetValue(hostLiteral, out SkillHostKind skillHost))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.HostUnsupported,
                $"The agent host does not support SKILL materialization: {hostLiteral}");
        }

        var planResult = CreateExportPlan(catalog, hostId, skillHost, cancellationToken);
        if (!planResult.IsSuccess)
        {
            return SkillOperationResult<string>.FailureResult(
                planResult.Failure!.Code,
                planResult.Failure.Message);
        }

        return format switch
        {
            SkillExportFormat.Directory => await ExportDirectoryAsync(planResult.Value!, outputPath, cancellationToken).ConfigureAwait(false),
            SkillExportFormat.Zip => await ExportZipAsync(planResult.Value!, outputPath, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Validated agent export format is unsupported: {format}"),
        };
    }

    private SkillOperationResult<IReadOnlyList<AgentExportEntry>> CreateExportPlan (
        AgentPackageCatalog catalog,
        AgentHostKind hostId,
        SkillHostKind skillHost,
        CancellationToken cancellationToken)
    {
        var entries = new List<AgentExportEntry>();
        var entryPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var agent in catalog.SelectedAgents.OrderBy(static agent => agent.Manifest.AgentName.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var artifacts = agent.Manifest.HostArtifacts
                .Where(artifact => artifact.HostId == hostId)
                .OrderBy(static artifact => artifact.Path, StringComparer.Ordinal)
                .ToArray();
            if (artifacts.Length == 0)
            {
                return SkillOperationResult<IReadOnlyList<AgentExportEntry>>.FailureResult(
                    SkillFailureCodes.HostUnsupported,
                    $"Agent '{agent.Manifest.AgentName.Value}' does not provide artifacts for host '{Vocabulary.GetText(hostId)}'.");
            }

            var fileByPath = agent.Files.ToDictionary(static file => file.RelativePath, StringComparer.Ordinal);
            var hostPathPrefix = $"hosts/{Vocabulary.GetText(hostId)}/";
            foreach (var artifact in artifacts)
            {
                if (!artifact.Path.StartsWith(hostPathPrefix, StringComparison.Ordinal)
                    || !fileByPath.TryGetValue(artifact.Path, out var artifactFile))
                {
                    return SkillOperationResult<IReadOnlyList<AgentExportEntry>>.FailureResult(
                        SkillFailureCodes.ManifestInvalid,
                        $"Agent host artifact does not match its package for '{agent.Manifest.AgentName.Value}': {artifact.Path}");
                }

                var hostRelativePath = artifact.Path[hostPathPrefix.Length..];
                var exportPath = $"agents/{hostRelativePath}";
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

            var materializationResult = skillMaterializationService.Materialize(skill, skillHost);
            if (!materializationResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<AgentExportEntry>>.FailureResult(
                    materializationResult.Failure!.Code,
                    materializationResult.Failure.Message);
            }

            foreach (var file in materializationResult.Value!.Files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal))
            {
                var exportPath = $"skills/{skill.Manifest.SkillName.Value}/{file.RelativePath}";
                var addResult = AddEntry(entries, entryPaths, exportPath, file.Content);
                if (!addResult.IsSuccess)
                {
                    return addResult;
                }
            }
        }

        return SkillOperationResult<IReadOnlyList<AgentExportEntry>>.Success(
            Array.AsReadOnly(entries.OrderBy(static entry => entry.RelativePath, StringComparer.Ordinal).ToArray()));
    }

    private static SkillOperationResult<IReadOnlyList<AgentExportEntry>> AddEntry (
        List<AgentExportEntry> entries,
        HashSet<string> entryPaths,
        string relativePath,
        string content)
    {
        if (!PackageRelativePath.TryParse(relativePath, out _))
        {
            return SkillOperationResult<IReadOnlyList<AgentExportEntry>>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Agent export path is unsafe: {relativePath}");
        }

        if (!entryPaths.Add(relativePath))
        {
            return SkillOperationResult<IReadOnlyList<AgentExportEntry>>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Agent export artifacts resolve to the same output path: {relativePath}");
        }

        entries.Add(new AgentExportEntry(relativePath, content));
        return SkillOperationResult<IReadOnlyList<AgentExportEntry>>.Success(entries);
    }

    private static async ValueTask<SkillOperationResult<string>> ExportDirectoryAsync (
        IReadOnlyList<AgentExportEntry> entries,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var outputResult = ResolveDirectoryOutputPath(outputPath);
        if (!outputResult.IsSuccess)
        {
            return outputResult;
        }

        var fullOutputPath = outputResult.Value!;
        var parentPath = Path.GetDirectoryName(fullOutputPath)!;
        var operationId = Guid.NewGuid().ToString("N");
        var stagingPath = Path.Combine(parentPath, $".{Path.GetFileName(fullOutputPath)}.staging.{operationId}");
        var backupPath = Path.Combine(parentPath, $".{Path.GetFileName(fullOutputPath)}.backup.{operationId}");
        var published = false;
        try
        {
            Directory.CreateDirectory(Path.Combine(stagingPath, "agents"));
            Directory.CreateDirectory(Path.Combine(stagingPath, "skills"));
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePathResult = SkillPackagePathBoundary.ResolvePackageFilePath(stagingPath, entry.RelativePath);
                if (!filePathResult.IsSuccess)
                {
                    return SkillOperationResult<string>.FailureResult(
                        filePathResult.Failure!.Code,
                        filePathResult.Failure.Message);
                }

                await SkillPackageFileWriter.WriteAllTextAtomicallyAsync(
                        filePathResult.Value!,
                        entry.Content,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            CanonicalSkillBundleDirectoryPublisher.Publish(stagingPath, fullOutputPath, backupPath);
            published = true;
            TryDeleteDirectory(backupPath);
            return SkillOperationResult<string>.Success(fullOutputPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"Failed to export agents to directory '{fullOutputPath}': {exception.Message}");
        }
        finally
        {
            if (!published)
            {
                TryDeleteDirectory(stagingPath);
            }
        }
    }

    private static async ValueTask<SkillOperationResult<string>> ExportZipAsync (
        IReadOnlyList<AgentExportEntry> entries,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var outputResult = ResolveZipOutputPath(outputPath);
        if (!outputResult.IsSuccess)
        {
            return outputResult;
        }

        var fullOutputPath = outputResult.Value!;
        var outputDirectory = Path.GetDirectoryName(fullOutputPath)!;
        var temporaryPath = Path.Combine(outputDirectory, $".{Path.GetFileName(fullOutputPath)}.{Guid.NewGuid():N}.tmp");
        var committed = false;
        try
        {
            Directory.CreateDirectory(outputDirectory);
            await using (var fileStream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8))
            {
                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var archiveEntry = archive.CreateEntry(entry.RelativePath, CompressionLevel.Optimal);
                    archiveEntry.LastWriteTime = ZipEntryTimestamp;
                    await using var entryStream = archiveEntry.Open();
                    var bytes = Encoding.UTF8.GetBytes(entry.Content);
                    await entryStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                }
            }

            File.Move(temporaryPath, fullOutputPath, overwrite: true);
            committed = true;
            return SkillOperationResult<string>.Success(fullOutputPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.InstallTargetWriteFailed,
                $"Failed to export agents to zip '{fullOutputPath}': {exception.Message}");
        }
        finally
        {
            if (!committed)
            {
                TryDeleteFile(temporaryPath);
            }
        }
    }

    private static SkillOperationResult<string> ResolveDirectoryOutputPath (string outputPath)
    {
        var fullOutputPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputPath));
        var parentPath = Path.GetDirectoryName(fullOutputPath);
        if (string.IsNullOrWhiteSpace(parentPath)
            || File.Exists(fullOutputPath)
            || (Directory.Exists(fullOutputPath) && !SkillPackageFileSystemEntryGuard.IsDirectory(fullOutputPath)))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Agent export output path must be a regular directory or an unused path: {fullOutputPath}");
        }

        return SkillPackagePathBoundary.ResolveUnderRoot(parentPath, fullOutputPath);
    }

    private static SkillOperationResult<string> ResolveZipOutputPath (string outputPath)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        var parentPath = Path.GetDirectoryName(fullOutputPath);
        if (string.IsNullOrWhiteSpace(parentPath)
            || Directory.Exists(fullOutputPath)
            || (File.Exists(fullOutputPath) && !SkillPackageFileSystemEntryGuard.IsRegularFile(fullOutputPath)))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Agent export zip output path must be a regular file or an unused path: {fullOutputPath}");
        }

        return SkillPackagePathBoundary.ResolveUnderRoot(parentPath, fullOutputPath);
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
            // Cleanup must not replace the authoritative export result.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup must not replace the authoritative export result.
        }
    }

    private static void TryDeleteFile (string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
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

    private sealed class AgentExportEntry
    {
        internal AgentExportEntry (string relativePath, string content)
        {
            RelativePath = relativePath;
            Content = content;
        }

        internal string RelativePath { get; }

        internal string Content { get; }
    }
}

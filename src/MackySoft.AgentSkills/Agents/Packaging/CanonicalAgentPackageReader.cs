using MackySoft.AgentSkills.Agents.Manifests;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Packaging.FileSystem;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Agents.Packaging;

/// <summary> Reads canonical generated agent packages below a v2 agents root. </summary>
internal sealed class CanonicalAgentPackageReader
{
    private readonly AgentManifestJsonSerializer serializer;
    private readonly SkillDigestCalculator digestCalculator;
    private readonly AgentManifestDigestCalculator manifestDigestCalculator;

    /// <summary> Initializes the reader. </summary>
    /// <param name="serializer"> The canonical agent manifest serializer. </param>
    /// <param name="digestCalculator"> The file digest calculator used to validate package content. </param>
    internal CanonicalAgentPackageReader (
        AgentManifestJsonSerializer serializer,
        SkillDigestCalculator digestCalculator,
        AgentManifestDigestCalculator manifestDigestCalculator)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
        this.manifestDigestCalculator = manifestDigestCalculator ?? throw new ArgumentNullException(nameof(manifestDigestCalculator));
    }

    /// <summary> Reads every agent package. </summary>
    /// <param name="agentsRoot"> The generated v2 agent package root. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through file access. </param>
    /// <returns>The canonical agent packages, or a manifest failure.</returns>
    internal async ValueTask<SkillOperationResult<IReadOnlyList<CanonicalAgentPackage>>> ReadAllAsync (
        string agentsRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentsRoot);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(agentsRoot))
        {
            return SkillOperationResult<IReadOnlyList<CanonicalAgentPackage>>.Success([]);
        }

        var fullAgentsRoot = Path.GetFullPath(agentsRoot);
        if (!SkillPackageFileSystemEntryGuard.IsDirectory(fullAgentsRoot))
        {
            return Failure($"Generated agents root must be a regular directory: {fullAgentsRoot}");
        }

        foreach (var rootEntry in Directory.EnumerateFileSystemEntries(fullAgentsRoot).Order(StringComparer.Ordinal))
        {
            if (!Directory.Exists(rootEntry) || !SkillPackageFileSystemEntryGuard.IsDirectory(rootEntry))
            {
                return Failure($"Generated agents root contains an unsupported entry: {Path.GetFileName(rootEntry)}");
            }
        }

        var packages = new List<CanonicalAgentPackage>();
        foreach (var directory in Directory.GetDirectories(fullAgentsRoot).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var packageResult = await ReadOneAsync(fullAgentsRoot, directory, cancellationToken).ConfigureAwait(false);
            if (!packageResult.IsSuccess)
            {
                return Failure(packageResult.Failure!.Message);
            }

            packages.Add(packageResult.Value!);
        }

        return SkillOperationResult<IReadOnlyList<CanonicalAgentPackage>>.Success(Array.AsReadOnly(packages.OrderBy(static package => package.Manifest.AgentName.Value, StringComparer.Ordinal).ToArray()));
    }

    private async ValueTask<SkillOperationResult<CanonicalAgentPackage>> ReadOneAsync (
        string agentsRoot,
        string agentDirectory,
        CancellationToken cancellationToken)
    {
        var directoryResult = SkillPackagePathBoundary.ResolveUnderRoot(agentsRoot, agentDirectory);
        if (!directoryResult.IsSuccess)
        {
            return PackageFailure(directoryResult.Failure!.Code, directoryResult.Failure.Message);
        }

        var filesResult = await ReadFilesAsync(directoryResult.Value!, cancellationToken).ConfigureAwait(false);
        if (!filesResult.IsSuccess)
        {
            return PackageFailure(filesResult.Failure!.Code, filesResult.Failure.Message);
        }

        try
        {
            var manifestFile = filesResult.Value!.SingleOrDefault(static file => file.RelativePath == "agent-manifest.json");
            if (manifestFile is null)
            {
                return PackageFailure(SkillFailureCodes.ManifestInvalid, "Generated agent package is missing agent-manifest.json.");
            }

            var manifest = serializer.Deserialize(manifestFile.Content);
            if (!string.Equals(manifestFile.Content, serializer.Serialize(manifest), StringComparison.Ordinal)
                || manifest.ManifestDigest != manifestDigestCalculator.ComputeManifestDigest(manifest))
            {
                return PackageFailure(SkillFailureCodes.ManifestInvalid, "agent-manifest.json is not canonical or its digest does not match manifest content.");
            }

            if (!string.Equals(Path.GetFileName(directoryResult.Value!), manifest.AgentName.Value, StringComparison.Ordinal))
            {
                return PackageFailure(SkillFailureCodes.ManifestInvalid, "agent-manifest.json agentName must match generated package directory name.");
            }

            return SkillOperationResult<CanonicalAgentPackage>.Success(new CanonicalAgentPackage(manifest, filesResult.Value!, serializer, digestCalculator));
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or ArgumentException or InvalidOperationException or FormatException)
        {
            return PackageFailure(SkillFailureCodes.ManifestInvalid, "Generated agent package is invalid.");
        }
    }

    private async ValueTask<SkillOperationResult<IReadOnlyList<SkillPackageFile>>> ReadFilesAsync (
        string agentDirectory,
        CancellationToken cancellationToken)
    {
        var files = new List<SkillPackageFile>();
        var result = await ReadDirectoryEntriesAsync(agentDirectory, agentDirectory, files, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? SkillOperationResult<IReadOnlyList<SkillPackageFile>>.Success(files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal).ToArray())
            : SkillOperationResult<IReadOnlyList<SkillPackageFile>>.FailureResult(result.Failure!.Code, result.Failure.Message);
    }

    private async ValueTask<SkillOperationResult<bool>> ReadDirectoryEntriesAsync (
        string agentDirectory,
        string directoryPath,
        List<SkillPackageFile> files,
        CancellationToken cancellationToken)
    {
        foreach (var entryPath in Directory.EnumerateFileSystemEntries(directoryPath).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(agentDirectory, entryPath).Replace(Path.DirectorySeparatorChar, '/');
            if (!PackageRelativePath.TryParse(relativePath, out _))
            {
                return BoolFailure(SkillFailureCodes.PathUnsafe, $"Generated agent package contains an unsafe path: {relativePath}");
            }

            if (Directory.Exists(entryPath))
            {
                if (!SkillPackageFileSystemEntryGuard.IsDirectory(entryPath))
                {
                    return BoolFailure(SkillFailureCodes.PathUnsafe, $"Generated agent package contains an unsupported non-regular directory: {relativePath}");
                }

                var childResult = SkillPackagePathBoundary.ResolveUnderRoot(agentDirectory, entryPath);
                if (!childResult.IsSuccess)
                {
                    return BoolFailure(childResult.Failure!.Code, childResult.Failure.Message);
                }

                var childReadResult = await ReadDirectoryEntriesAsync(agentDirectory, childResult.Value!, files, cancellationToken).ConfigureAwait(false);
                if (!childReadResult.IsSuccess)
                {
                    return childReadResult;
                }

                continue;
            }

            if (!File.Exists(entryPath) || !SkillPackageFileSystemEntryGuard.IsRegularFile(entryPath))
            {
                return BoolFailure(SkillFailureCodes.PathUnsafe, $"Generated agent package contains an unsupported non-regular file: {relativePath}");
            }

            var fileResult = SkillPackagePathBoundary.ResolveUnderRoot(agentDirectory, entryPath);
            if (!fileResult.IsSuccess)
            {
                return BoolFailure(fileResult.Failure!.Code, fileResult.Failure.Message);
            }

            var textResult = await SkillPackageTextFileReader.ReadAsync(fileResult.Value!, cancellationToken).ConfigureAwait(false);
            if (!textResult.IsSuccess)
            {
                return BoolFailure(textResult.Failure!.Code, textResult.Failure.Message);
            }

            files.Add(new SkillPackageFile(relativePath, textResult.Value!));
        }

        return SkillOperationResult<bool>.Success(true);
    }

    private static SkillOperationResult<IReadOnlyList<CanonicalAgentPackage>> Failure (string message) => SkillOperationResult<IReadOnlyList<CanonicalAgentPackage>>.FailureResult(SkillFailureCodes.ManifestInvalid, message);

    private static SkillOperationResult<CanonicalAgentPackage> PackageFailure (SkillFailureCode code, string message) => SkillOperationResult<CanonicalAgentPackage>.FailureResult(code, message);

    private static SkillOperationResult<bool> BoolFailure (SkillFailureCode code, string message) => SkillOperationResult<bool>.FailureResult(code, message);
}

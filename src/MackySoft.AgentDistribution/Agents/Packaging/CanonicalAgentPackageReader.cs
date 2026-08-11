using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Packaging.Canonical;
using MackySoft.AgentDistribution.Packaging.Paths;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Packaging;

/// <summary> Reads canonical generated agent packages below a v3 agents root. </summary>
internal sealed class CanonicalAgentPackageReader
{
    private static readonly PackageRelativePath ManifestPath = PackageRelativePath.Parse("agent-manifest.json");

    private readonly AgentManifestJsonSerializer serializer;
    private readonly PackageContentDigestCalculator digestCalculator;
    private readonly AgentManifestDigestCalculator manifestDigestCalculator;

    /// <summary> Initializes the reader. </summary>
    /// <param name="serializer"> The canonical agent manifest serializer. </param>
    /// <param name="digestCalculator"> The file digest calculator used to validate package content. </param>
    internal CanonicalAgentPackageReader (
        AgentManifestJsonSerializer serializer,
        PackageContentDigestCalculator digestCalculator,
        AgentManifestDigestCalculator manifestDigestCalculator)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
        this.manifestDigestCalculator = manifestDigestCalculator ?? throw new ArgumentNullException(nameof(manifestDigestCalculator));
    }

    /// <summary> Reads every agent package. </summary>
    /// <param name="agentsRoot"> The generated v3 agent package root. </param>
    /// <param name="cancellationToken"> The cancellation token propagated through file access. </param>
    /// <returns>The canonical agent packages, or a manifest failure.</returns>
    internal async ValueTask<AgentDistributionOperationResult<IReadOnlyList<CanonicalAgentPackage>>> ReadAllAsync (
        AbsolutePath agentsRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agentsRoot);
        cancellationToken.ThrowIfCancellationRequested();
        var fullAgentsRoot = agentsRoot;
        if (!FileSystemEntryInspector.TryInspect(
                fullAgentsRoot,
                out var agentsRootObservation,
                out _))
        {
            return AgentDistributionOperationResult<IReadOnlyList<CanonicalAgentPackage>>.FailureResult(
                AgentDistributionFailureCodes.PathUnsafe,
                $"Generated agents root could not be inspected: {fullAgentsRoot.Value}");
        }

        if (agentsRootObservation.State == FileSystemEntryState.Missing)
        {
            return AgentDistributionOperationResult<IReadOnlyList<CanonicalAgentPackage>>.Success([]);
        }

        if (agentsRootObservation.State != FileSystemEntryState.Directory)
        {
            return Failure($"Generated agents root must be a regular directory: {fullAgentsRoot.Value}");
        }

        foreach (var rootEntry in Directory.EnumerateFileSystemEntries(fullAgentsRoot.Value).Order(StringComparer.Ordinal))
        {
            if (!FileSystemEntryInspector.TryInspect(
                    AbsolutePath.Parse(rootEntry),
                    out var rootEntryObservation,
                    out _)
                || rootEntryObservation.State != FileSystemEntryState.Directory)
            {
                return Failure($"Generated agents root contains an unsupported entry: {Path.GetFileName(rootEntry)}");
            }
        }

        var packages = new List<CanonicalAgentPackage>();
        foreach (var directory in Directory.GetDirectories(fullAgentsRoot.Value).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var packageResult = await ReadOneAsync(fullAgentsRoot, AbsolutePath.Parse(directory), cancellationToken).ConfigureAwait(false);
            if (!packageResult.IsSuccess)
            {
                return Failure(packageResult.Failure!.Message);
            }

            packages.Add(packageResult.Value!);
        }

        return AgentDistributionOperationResult<IReadOnlyList<CanonicalAgentPackage>>.Success(Array.AsReadOnly(packages.OrderBy(static package => package.Manifest.AgentName.Value, StringComparer.Ordinal).ToArray()));
    }

    private async ValueTask<AgentDistributionOperationResult<CanonicalAgentPackage>> ReadOneAsync (
        AbsolutePath agentsRoot,
        AbsolutePath agentDirectory,
        CancellationToken cancellationToken)
    {
        var directoryResult = PackagePathResolver.ResolveUnderRoot(agentsRoot, agentDirectory);
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
            var manifestFile = filesResult.Value!.SingleOrDefault(static file => file.RelativePath == ManifestPath);
            if (manifestFile is null)
            {
                return PackageFailure(AgentDistributionFailureCodes.ManifestInvalid, "Generated agent package is missing agent-manifest.json.");
            }

            var manifest = serializer.Deserialize(manifestFile.Content);
            if (!string.Equals(manifestFile.Content, serializer.Serialize(manifest), StringComparison.Ordinal)
                || manifest.ManifestDigest != manifestDigestCalculator.ComputeManifestDigest(manifest))
            {
                return PackageFailure(AgentDistributionFailureCodes.ManifestInvalid, "agent-manifest.json is not canonical or its digest does not match manifest content.");
            }

            if (!string.Equals(Path.GetFileName(directoryResult.Value!.Value), manifest.AgentName.Value, StringComparison.Ordinal))
            {
                return PackageFailure(AgentDistributionFailureCodes.ManifestInvalid, "agent-manifest.json agentName must match generated package directory name.");
            }

            return AgentDistributionOperationResult<CanonicalAgentPackage>.Success(new CanonicalAgentPackage(manifest, filesResult.Value!, serializer, digestCalculator));
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or ArgumentException or InvalidOperationException or FormatException)
        {
            return PackageFailure(AgentDistributionFailureCodes.ManifestInvalid, "Generated agent package is invalid.");
        }
    }

    private async ValueTask<AgentDistributionOperationResult<IReadOnlyList<PackageTextFile>>> ReadFilesAsync (
        AbsolutePath agentDirectory,
        CancellationToken cancellationToken)
    {
        var files = new List<PackageTextFile>();
        var result = await ReadDirectoryEntriesAsync(agentDirectory, agentDirectory, files, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? AgentDistributionOperationResult<IReadOnlyList<PackageTextFile>>.Success(files.OrderBy(static file => file.RelativePath.Value, StringComparer.Ordinal).ToArray())
            : AgentDistributionOperationResult<IReadOnlyList<PackageTextFile>>.FailureResult(result.Failure!.Code, result.Failure.Message);
    }

    private async ValueTask<AgentDistributionOperationResult<bool>> ReadDirectoryEntriesAsync (
        AbsolutePath agentDirectory,
        AbsolutePath directoryPath,
        List<PackageTextFile> files,
        CancellationToken cancellationToken)
    {
        foreach (var entryPathText in Directory.EnumerateFileSystemEntries(directoryPath.Value).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entryPath = AbsolutePath.Parse(entryPathText);
            var relativePath = Path.GetRelativePath(agentDirectory.Value, entryPath.Value).Replace(Path.DirectorySeparatorChar, '/');
            if (!PackageRelativePath.TryParse(relativePath, out _))
            {
                return BoolFailure(AgentDistributionFailureCodes.PathUnsafe, $"Generated agent package contains an unsafe path: {relativePath}");
            }

            if (!FileSystemEntryInspector.TryInspect(
                    entryPath,
                    out var entryObservation,
                    out _))
            {
                return BoolFailure(AgentDistributionFailureCodes.PathUnsafe, $"Generated agent package contains an unsupported non-regular path: {relativePath}");
            }

            if (entryObservation.State == FileSystemEntryState.Directory)
            {
                var childResult = PackagePathResolver.ResolveUnderRoot(agentDirectory, entryPath);
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

            if (entryObservation.State != FileSystemEntryState.RegularFile)
            {
                return BoolFailure(AgentDistributionFailureCodes.PathUnsafe, $"Generated agent package contains an unsupported non-regular path: {relativePath}");
            }

            var fileResult = PackagePathResolver.ResolveUnderRoot(agentDirectory, entryPath);
            if (!fileResult.IsSuccess)
            {
                return BoolFailure(fileResult.Failure!.Code, fileResult.Failure.Message);
            }

            var textResult = await CanonicalPackageTextReader.ReadAsync(fileResult.Value!, cancellationToken).ConfigureAwait(false);
            if (!textResult.IsSuccess)
            {
                return BoolFailure(textResult.Failure!.Code, textResult.Failure.Message);
            }

            files.Add(new PackageTextFile(PackageRelativePath.Parse(relativePath), textResult.Value!));
        }

        return AgentDistributionOperationResult<bool>.Success(true);
    }

    private static AgentDistributionOperationResult<IReadOnlyList<CanonicalAgentPackage>> Failure (string message) => AgentDistributionOperationResult<IReadOnlyList<CanonicalAgentPackage>>.FailureResult(AgentDistributionFailureCodes.ManifestInvalid, message);

    private static AgentDistributionOperationResult<CanonicalAgentPackage> PackageFailure (AgentDistributionFailureCode code, string message) => AgentDistributionOperationResult<CanonicalAgentPackage>.FailureResult(code, message);

    private static AgentDistributionOperationResult<bool> BoolFailure (AgentDistributionFailureCode code, string message) => AgentDistributionOperationResult<bool>.FailureResult(code, message);
}

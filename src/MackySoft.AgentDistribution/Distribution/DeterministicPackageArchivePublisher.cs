using System.IO.Compression;
using System.Text;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Distribution;

/// <summary>Publishes one deterministic ZIP representation of a package file set.</summary>
internal static class DeterministicPackageArchivePublisher
{
    private static readonly DateTimeOffset EntryTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Serializes sorted package entries and publishes the archive through the Foundation atomic-file contract.</summary>
    internal static async ValueTask PublishAsync (
        AbsolutePath outputPath,
        IReadOnlyList<PackageTextFile> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(entries);
        cancellationToken.ThrowIfCancellationRequested();

        using var contentsStream = new MemoryStream();
        using (var archive = new ZipArchive(contentsStream, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            foreach (var entry in entries.OrderBy(static entry => entry.RelativePath.Value, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentNullException.ThrowIfNull(entry);

                var archiveEntry = archive.CreateEntry(entry.RelativePath.Value, CompressionLevel.Optimal);
                archiveEntry.LastWriteTime = EntryTimestamp;
                await using var entryStream = archiveEntry.Open();
                var bytes = Encoding.UTF8.GetBytes(entry.Content);
                await entryStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            }
        }

        contentsStream.Position = 0;
        if (!outputPath.TryGetParent(out var outputDirectory))
        {
            throw new ArgumentException("A package archive target must have a parent directory.", nameof(outputPath));
        }

        var publication = new AtomicFilePublication(
            ContainedPath.Create(outputDirectory, outputPath),
            SymbolicLinkHandling.Reject,
            ExistingTargetHandling.Replace,
            MissingParentHandling.Create);
        var result = await AtomicFilePublisher
            .PublishAsync(publication, contentsStream, cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new IOException($"Package archive publication failed: {result.Failure}");
        }
    }
}

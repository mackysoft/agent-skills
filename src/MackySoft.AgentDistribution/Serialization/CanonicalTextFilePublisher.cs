using System.Text;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Serialization;

/// <summary>Publishes canonical Agent Distribution text as strict UTF-8 without a byte order mark.</summary>
internal static class CanonicalTextFilePublisher
{
    private static readonly UTF8Encoding Utf8NoBom = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    /// <summary>Publishes complete canonical text through the Foundation atomic-file contract.</summary>
    internal static async ValueTask PublishAsync (
        AbsolutePath path,
        string contents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(contents);
        cancellationToken.ThrowIfCancellationRequested();

        if (!path.TryGetParent(out var targetDirectory))
        {
            throw new ArgumentException("A canonical text target must have a parent directory.", nameof(path));
        }

        using var contentsStream = new MemoryStream(Utf8NoBom.GetBytes(contents), writable: false);
        var publication = new AtomicFilePublication(
            ContainedPath.Create(targetDirectory, path),
            SymbolicLinkHandling.Reject,
            ExistingTargetHandling.Replace,
            MissingParentHandling.Create);
        var result = await AtomicFilePublisher
            .PublishAsync(publication, contentsStream, cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new IOException($"Canonical text publication failed: {result.Failure}");
        }
    }
}

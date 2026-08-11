using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Digests;

/// <summary> Represents one normalized file used for package content digest input. </summary>
public sealed class PackageContentDigestInputFile
{
    /// <summary> Initializes one validated, normalized digest input file. </summary>
    /// <param name="relativePath"> The slash-separated relative path. </param>
    /// <param name="content"> The text content to normalize to LF line endings. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="relativePath" /> is not a safe relative file path. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="content" /> is <see langword="null" />. </exception>
    public PackageContentDigestInputFile (
        PackageRelativePath relativePath,
        string content)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(content);

        RelativePath = relativePath;
        Content = AgentDistributionTextNormalizer.NormalizeToLf(content);
    }

    /// <summary> Gets the slash-separated relative path. </summary>
    public PackageRelativePath RelativePath { get; }

    /// <summary> Gets the content normalized to LF line endings. </summary>
    public string Content { get; }
}

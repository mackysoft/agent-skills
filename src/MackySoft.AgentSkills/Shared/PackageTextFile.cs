namespace MackySoft.AgentSkills.Shared;

/// <summary> Represents one deterministic text file in a distributable package. </summary>
public sealed class PackageTextFile
{
    /// <summary> Initializes one package file with a safe path and canonical text content. </summary>
    /// <param name="relativePath"> The slash-separated package-relative path. </param>
    /// <param name="content"> The UTF-8 text content, normalized to LF line endings. </param>
    public PackageTextFile (
        PackageRelativePath relativePath,
        string content)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        if (content.StartsWith('\uFEFF'))
        {
            throw new ArgumentException("Package file content must not start with a UTF-8 byte order mark.", nameof(content));
        }

        RelativePath = relativePath;
        Content = SkillTextNormalizer.NormalizeToLf(content);
    }

    /// <summary> Gets the slash-separated package-relative path. </summary>
    public PackageRelativePath RelativePath { get; }

    /// <summary> Gets the text content normalized to LF line endings. </summary>
    public string Content { get; }
}

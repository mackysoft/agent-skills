using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Digests;

/// <summary> Represents one normalized file used for SKILL digest input. </summary>
public sealed class SkillDigestInputFile
{
    /// <summary> Initializes one validated, normalized digest input file. </summary>
    /// <param name="relativePath"> The slash-separated relative path. </param>
    /// <param name="content"> The text content to normalize to LF line endings. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="relativePath" /> is not a safe relative file path. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="content" /> is <see langword="null" />. </exception>
    public SkillDigestInputFile (
        string relativePath,
        string content)
    {
        if (!SkillRelativePath.IsSafeFilePath(relativePath))
        {
            throw new ArgumentException("Digest file path must be a safe slash-separated relative path.", nameof(relativePath));
        }

        ArgumentNullException.ThrowIfNull(content);

        RelativePath = relativePath;
        Content = SkillTextNormalizer.NormalizeToLf(content);
    }

    /// <summary> Gets the slash-separated relative path. </summary>
    public string RelativePath { get; }

    /// <summary> Gets the content normalized to LF line endings. </summary>
    public string Content { get; }
}

using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Sources;

/// <summary> Represents one source reference template. </summary>
internal sealed class SkillSourceReference
{
    /// <summary> Initializes one source reference template. </summary>
    /// <param name="fileName"> The generated reference file name. </param>
    /// <param name="template"> The source template text. </param>
    internal SkillSourceReference (
        string fileName,
        string template)
    {
        if (!IsValidFileName(fileName))
        {
            throw new ArgumentException("Reference file name must be a safe Markdown file name.", nameof(fileName));
        }

        FileName = fileName;
        Template = template ?? throw new ArgumentNullException(nameof(template));
    }

    /// <summary> Gets the generated reference file name. </summary>
    internal string FileName { get; }

    /// <summary> Gets the source template text. </summary>
    internal string Template { get; }

    internal static bool IsValidFileName (string? fileName)
    {
        return fileName?.EndsWith(".md", StringComparison.Ordinal) == true
            && SkillRelativePath.IsSafePathSegment(fileName);
    }
}

namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents one structured file difference. </summary>
public sealed class SkillFileDiff
{
    /// <summary> Initializes one structured file difference. </summary>
    internal SkillFileDiff (
        string relativePath,
        SkillDiffChangeKind changeKind,
        string? beforeContent,
        string? afterContent)
    {
        SkillActionContractGuard.ValidateRelativePath(relativePath, nameof(relativePath));
        RelativePath = relativePath;
        ChangeKind = SkillActionContractGuard.ValidateEnum(changeKind, nameof(changeKind));
        BeforeContent = beforeContent;
        AfterContent = afterContent;

        var hasValidContents = ChangeKind switch
        {
            SkillDiffChangeKind.Added => BeforeContent is null && AfterContent is not null,
            SkillDiffChangeKind.Modified => BeforeContent is not null && AfterContent is not null,
            SkillDiffChangeKind.Deleted => BeforeContent is not null && AfterContent is null,
            _ => false,
        };
        if (!hasValidContents)
        {
            throw new ArgumentException("The before and after contents must match the file diff change kind.", nameof(changeKind));
        }
    }

    /// <summary> Gets the slash-separated path relative to the SKILL directory. </summary>
    public string RelativePath { get; }

    /// <summary> Gets the file change kind. </summary>
    public SkillDiffChangeKind ChangeKind { get; }

    /// <summary> Gets the previous content, or <see langword="null" /> for an added file. </summary>
    public string? BeforeContent { get; }

    /// <summary> Gets the next content, or <see langword="null" /> for a deleted file. </summary>
    public string? AfterContent { get; }
}

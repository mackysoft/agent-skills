using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents one file diff. </summary>
public sealed class SkillOperationFileDiffReport
{
    internal SkillOperationFileDiffReport (
        string relativePath,
        SkillDiffChangeKind changeKind,
        string? beforeContent,
        string? afterContent)
    {
        OperationReportContractGuard.ValidateSafeRelativePath(relativePath, nameof(relativePath));
        if (!ContractLiteralCodec.IsDefined(changeKind))
        {
            throw new ArgumentOutOfRangeException(nameof(changeKind), changeKind, "Unsupported diff change kind.");
        }

        ValidateContentShape(changeKind, beforeContent, afterContent);

        RelativePath = relativePath;
        ChangeKind = changeKind;
        BeforeContent = beforeContent;
        AfterContent = afterContent;
    }

    /// <summary> Gets the slash-separated path relative to the skill directory. </summary>
    public string RelativePath { get; }

    /// <summary> Gets the file change kind. </summary>
    public SkillDiffChangeKind ChangeKind { get; }

    /// <summary> Gets the previous file content, or <see langword="null" /> for added files. </summary>
    public string? BeforeContent { get; }

    /// <summary> Gets the next file content, or <see langword="null" /> for deleted files. </summary>
    public string? AfterContent { get; }

    private static void ValidateContentShape (
        SkillDiffChangeKind changeKind,
        string? beforeContent,
        string? afterContent)
    {
        var isValid = changeKind switch
        {
            SkillDiffChangeKind.Added => beforeContent is null && afterContent is not null,
            SkillDiffChangeKind.Modified => beforeContent is not null && afterContent is not null,
            SkillDiffChangeKind.Deleted => beforeContent is not null && afterContent is null,
            _ => false,
        };

        if (!isValid)
        {
            throw new ArgumentException("File diff contents must match the added, modified, or deleted change kind.", nameof(changeKind));
        }
    }
}

namespace MackySoft.AgentSkills.OperationReports;

/// <summary> Represents one file diff with a stable change-kind literal. </summary>
/// <param name="RelativePath"> The slash-separated path relative to the skill directory. </param>
/// <param name="ChangeKind"> The stable file change-kind literal. </param>
/// <param name="BeforeContent"> The previous file content, or <see langword="null" /> for added files. </param>
/// <param name="AfterContent"> The next file content, or <see langword="null" /> for deleted files. </param>
public sealed record SkillOperationFileDiffReport (
    string RelativePath,
    string ChangeKind,
    string? BeforeContent,
    string? AfterContent);

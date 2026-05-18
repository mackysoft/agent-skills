namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents one structured file difference. </summary>
/// <param name="RelativePath"> The slash-separated path relative to the skill directory. </param>
/// <param name="ChangeKind"> The change kind. </param>
/// <param name="BeforeContent"> The previous file content, or <see langword="null" /> for additions. </param>
/// <param name="AfterContent"> The next file content, or <see langword="null" /> for deletions. </param>
public sealed record SkillFileDiff (
    string RelativePath,
    SkillDiffChangeKind ChangeKind,
    string? BeforeContent,
    string? AfterContent);

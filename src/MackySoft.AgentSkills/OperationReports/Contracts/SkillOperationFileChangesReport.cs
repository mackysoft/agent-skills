namespace MackySoft.AgentSkills.OperationReports;

/// <summary> Represents existing target files that an operation replaces or removes. </summary>
/// <param name="ReplacedFiles"> Existing files replaced by the operation, sorted using ordinal comparison. </param>
/// <param name="RemovedFiles"> Existing files removed by the operation, sorted using ordinal comparison. </param>
public sealed record SkillOperationFileChangesReport (
    IReadOnlyList<string> ReplacedFiles,
    IReadOnlyList<string> RemovedFiles);

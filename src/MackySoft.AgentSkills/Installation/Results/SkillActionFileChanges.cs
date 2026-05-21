namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents deterministic file changes planned or performed by one SKILL action. </summary>
/// <param name="ReplacedFiles"> Existing package-relative files whose content is replaced by the action. </param>
/// <param name="RemovedFiles"> Existing package-relative files removed by the action. </param>
public sealed record SkillActionFileChanges (
    IReadOnlyList<string> ReplacedFiles,
    IReadOnlyList<string> RemovedFiles);

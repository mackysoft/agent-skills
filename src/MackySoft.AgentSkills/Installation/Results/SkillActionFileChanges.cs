namespace MackySoft.AgentSkills.Installation.Results;

/// <summary>
/// Represents the existing target files that a create, replace, or delete action plans or performs.
/// </summary>
/// <param name="ReplacedFiles">
/// Existing slash-separated paths, relative to the skill directory, whose file content differs from the
/// materialized package file at the same path. Paths are ordinal sorted and do not include added files.
/// </param>
/// <param name="RemovedFiles">
/// Existing slash-separated paths, relative to the skill directory, that the action removes.
/// Paths are ordinal sorted and do not include directories.
/// </param>
public sealed record SkillActionFileChanges (
    IReadOnlyList<string> ReplacedFiles,
    IReadOnlyList<string> RemovedFiles);

namespace MackySoft.AgentSkills.OperationReports;

/// <summary> Represents structured file-set drift details for a target state. </summary>
/// <param name="MissingFiles"> Expected managed files that are missing from the target. </param>
/// <param name="ExtraFiles"> Installed package-relative files that are not part of the expected file set. </param>
/// <param name="ExtraDirectories"> Installed package-relative directories that are not explained by expected or installed files. </param>
public sealed record SkillTargetFileSetReport (
    IReadOnlyList<string> MissingFiles,
    IReadOnlyList<string> ExtraFiles,
    IReadOnlyList<string> ExtraDirectories);

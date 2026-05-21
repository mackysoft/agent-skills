namespace MackySoft.AgentSkills.Installation.State;

/// <summary> Represents structured file-set drift details for an installed target state. </summary>
/// <param name="MissingFiles"> Expected package-relative files that are absent from the installed directory. </param>
/// <param name="ExtraFiles"> Installed package-relative files that are not part of the expected file set. </param>
/// <param name="ExtraDirectories"> Installed package-relative directories that are not explained by expected or installed files. </param>
public sealed record SkillInstalledTargetFileSet (
    IReadOnlyList<string> MissingFiles,
    IReadOnlyList<string> ExtraFiles,
    IReadOnlyList<string> ExtraDirectories);

namespace MackySoft.AgentSkills.Installation.Validation;

/// <summary> Describes installed file-set drift for one materialized SKILL package. </summary>
/// <param name="MissingFiles"> Expected package-relative files that are absent from the installed directory. </param>
/// <param name="ExtraFiles"> Installed package-relative files that are not part of the expected file set. </param>
/// <param name="MismatchedFiles"> Expected package-relative files whose normalized content does not match. </param>
/// <param name="ExtraDirectories"> Installed package-relative directories that are not explained by expected or installed files. </param>
public sealed record SkillInstalledFileSetVerificationResult (
    IReadOnlyList<string> MissingFiles,
    IReadOnlyList<string> ExtraFiles,
    IReadOnlyList<string> MismatchedFiles,
    IReadOnlyList<string> ExtraDirectories)
{
    /// <summary> Gets a value indicating whether the installed directory exactly matches the expected file set and content. </summary>
    public bool IsExactMatch =>
        MissingFiles.Count == 0
        && ExtraFiles.Count == 0
        && MismatchedFiles.Count == 0
        && ExtraDirectories.Count == 0;

    /// <summary> Gets a value indicating whether the installed file or directory set drifted. </summary>
    public bool HasFileSetDrift =>
        MissingFiles.Count != 0
        || ExtraFiles.Count != 0
        || ExtraDirectories.Count != 0;

    /// <summary> Gets a value indicating whether expected installed file content drifted. </summary>
    public bool HasContentDrift => MismatchedFiles.Count != 0;
}

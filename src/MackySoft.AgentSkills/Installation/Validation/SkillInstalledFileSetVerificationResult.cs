namespace MackySoft.AgentSkills.Installation.Validation;

/// <summary> Describes installed file-set drift for one materialized SKILL package. </summary>
public sealed class SkillInstalledFileSetVerificationResult
{
    /// <summary> Initializes one installed file-set verification result. </summary>
    /// <param name="missingFiles"> Expected package-relative files that are absent from the installed directory. </param>
    /// <param name="extraFiles"> Installed package-relative files that are not part of the expected file set. </param>
    /// <param name="extraDirectories"> Installed package-relative directories that are not explained by expected or installed files. </param>
    internal SkillInstalledFileSetVerificationResult (
        IReadOnlyList<string> missingFiles,
        IReadOnlyList<string> extraFiles,
        IReadOnlyList<string> extraDirectories)
    {
        MissingFiles = SkillInstalledFileSetPathSnapshot.Create(missingFiles, nameof(missingFiles));
        ExtraFiles = SkillInstalledFileSetPathSnapshot.Create(extraFiles, nameof(extraFiles));
        ExtraDirectories = SkillInstalledFileSetPathSnapshot.Create(extraDirectories, nameof(extraDirectories));
    }

    /// <summary> Gets expected package-relative files that are absent from the installed directory. </summary>
    public IReadOnlyList<string> MissingFiles { get; }

    /// <summary> Gets installed package-relative files that are not part of the expected file set. </summary>
    public IReadOnlyList<string> ExtraFiles { get; }

    /// <summary> Gets installed package-relative directories that are not explained by expected or installed files. </summary>
    public IReadOnlyList<string> ExtraDirectories { get; }

    /// <summary> Gets a value indicating whether the installed directory exactly matches the expected file set. </summary>
    public bool IsExactMatch =>
        MissingFiles.Count == 0
        && ExtraFiles.Count == 0
        && ExtraDirectories.Count == 0;

    /// <summary> Gets a value indicating whether the installed file or directory set drifted. </summary>
    public bool HasFileSetDrift => !IsExactMatch;
}

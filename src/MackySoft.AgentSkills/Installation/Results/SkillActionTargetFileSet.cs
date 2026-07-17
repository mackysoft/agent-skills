namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents structured file-set drift details attached to an action target state. </summary>
public sealed class SkillActionTargetFileSet
{
    /// <summary> Initializes structured file-set drift details. </summary>
    internal SkillActionTargetFileSet (
        IReadOnlyList<string> missingFiles,
        IReadOnlyList<string> extraFiles,
        IReadOnlyList<string> extraDirectories)
    {
        MissingFiles = SkillActionContractGuard.PathSnapshot(missingFiles, nameof(missingFiles));
        ExtraFiles = SkillActionContractGuard.PathSnapshot(extraFiles, nameof(extraFiles));
        ExtraDirectories = SkillActionContractGuard.PathSnapshot(extraDirectories, nameof(extraDirectories));
    }

    /// <summary> Gets expected files that are absent. </summary>
    public IReadOnlyList<string> MissingFiles { get; }

    /// <summary> Gets installed files that are not expected. </summary>
    public IReadOnlyList<string> ExtraFiles { get; }

    /// <summary> Gets installed directories that are not expected. </summary>
    public IReadOnlyList<string> ExtraDirectories { get; }
}

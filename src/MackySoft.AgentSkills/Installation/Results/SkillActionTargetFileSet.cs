namespace MackySoft.AgentSkills.Installation.Results;

using MackySoft.AgentSkills.Shared;

/// <summary> Represents structured file-set drift details attached to an action target state. </summary>
public sealed class SkillActionTargetFileSet
{
    /// <summary> Initializes structured file-set drift details. </summary>
    internal SkillActionTargetFileSet (
        IReadOnlyList<PackageRelativePath> missingFiles,
        IReadOnlyList<PackageRelativePath> extraFiles,
        IReadOnlyList<PackageRelativePath> extraDirectories)
    {
        MissingFiles = SkillActionContractGuard.PathSnapshot(missingFiles, nameof(missingFiles));
        ExtraFiles = SkillActionContractGuard.PathSnapshot(extraFiles, nameof(extraFiles));
        ExtraDirectories = SkillActionContractGuard.PathSnapshot(extraDirectories, nameof(extraDirectories));
    }

    /// <summary> Gets expected files that are absent. </summary>
    public IReadOnlyList<PackageRelativePath> MissingFiles { get; }

    /// <summary> Gets installed files that are not expected. </summary>
    public IReadOnlyList<PackageRelativePath> ExtraFiles { get; }

    /// <summary> Gets installed directories that are not expected. </summary>
    public IReadOnlyList<PackageRelativePath> ExtraDirectories { get; }
}

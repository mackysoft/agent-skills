using MackySoft.AgentSkills.Installation.Validation;

namespace MackySoft.AgentSkills.Installation.State;

/// <summary> Represents structured file-set drift details for an installed target state. </summary>
public sealed class SkillInstalledTargetFileSet
{
    /// <summary> Initializes structured file-set drift details for an installed target state. </summary>
    /// <param name="missingFiles"> Expected package-relative files that are absent from the installed directory. </param>
    /// <param name="extraFiles"> Installed package-relative files that are not part of the expected file set. </param>
    /// <param name="extraDirectories"> Installed package-relative directories that are not explained by expected or installed files. </param>
    internal SkillInstalledTargetFileSet (
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
}

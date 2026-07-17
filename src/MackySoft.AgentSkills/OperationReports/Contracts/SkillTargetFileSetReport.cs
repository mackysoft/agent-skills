namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents structured file-set drift details for a target state. </summary>
public sealed class SkillTargetFileSetReport
{
    internal SkillTargetFileSetReport (
        IReadOnlyList<string> missingFiles,
        IReadOnlyList<string> extraFiles,
        IReadOnlyList<string> extraDirectories)
    {
        MissingFiles = OperationReportContractGuard.SnapshotSafeRelativePaths(missingFiles, nameof(missingFiles));
        ExtraFiles = OperationReportContractGuard.SnapshotSafeRelativePaths(extraFiles, nameof(extraFiles));
        ExtraDirectories = OperationReportContractGuard.SnapshotSafeRelativePaths(extraDirectories, nameof(extraDirectories));
    }

    /// <summary> Gets expected managed files that are missing from the target. </summary>
    public IReadOnlyList<string> MissingFiles { get; }

    /// <summary> Gets installed package-relative files that are not part of the expected file set. </summary>
    public IReadOnlyList<string> ExtraFiles { get; }

    /// <summary> Gets installed package-relative directories that are not explained by expected or installed files. </summary>
    public IReadOnlyList<string> ExtraDirectories { get; }
}

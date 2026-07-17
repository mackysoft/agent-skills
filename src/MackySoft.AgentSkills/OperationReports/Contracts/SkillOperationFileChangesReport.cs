namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents existing target files that an operation replaces or removes. </summary>
public sealed class SkillOperationFileChangesReport
{
    internal SkillOperationFileChangesReport (
        IReadOnlyList<string> replacedFiles,
        IReadOnlyList<string> removedFiles)
    {
        ReplacedFiles = OperationReportContractGuard.SnapshotSafeRelativePaths(replacedFiles, nameof(replacedFiles));
        RemovedFiles = OperationReportContractGuard.SnapshotSafeRelativePaths(removedFiles, nameof(removedFiles));
    }

    /// <summary> Gets existing files replaced by the operation, sorted using ordinal comparison. </summary>
    public IReadOnlyList<string> ReplacedFiles { get; }

    /// <summary> Gets existing files removed by the operation, sorted using ordinal comparison. </summary>
    public IReadOnlyList<string> RemovedFiles { get; }
}

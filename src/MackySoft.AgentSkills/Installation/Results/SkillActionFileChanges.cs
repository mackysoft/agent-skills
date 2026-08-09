namespace MackySoft.AgentSkills.Installation.Results;

using MackySoft.AgentSkills.Shared;

/// <summary>
/// Represents the existing target files that a create, replace, or delete action plans or performs.
/// </summary>
public sealed class SkillActionFileChanges
{
    /// <summary> Initializes one existing file change summary. </summary>
    internal SkillActionFileChanges (
        IReadOnlyList<PackageRelativePath> replacedFiles,
        IReadOnlyList<PackageRelativePath> removedFiles)
    {
        ReplacedFiles = SkillActionContractGuard.PathSnapshot(replacedFiles, nameof(replacedFiles), sortOrdinal: true);
        RemovedFiles = SkillActionContractGuard.PathSnapshot(removedFiles, nameof(removedFiles), sortOrdinal: true);
        if (ReplacedFiles.Intersect(RemovedFiles).Any())
        {
            throw new ArgumentException("A file cannot be both replaced and removed by the same action.", nameof(removedFiles));
        }
    }

    /// <summary> Gets an ordinal-sorted snapshot of existing files whose content is replaced. </summary>
    public IReadOnlyList<PackageRelativePath> ReplacedFiles { get; }

    /// <summary> Gets an ordinal-sorted snapshot of existing files that are removed. </summary>
    public IReadOnlyList<PackageRelativePath> RemovedFiles { get; }
}

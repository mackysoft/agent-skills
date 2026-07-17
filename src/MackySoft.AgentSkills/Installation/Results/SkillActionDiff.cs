namespace MackySoft.AgentSkills.Installation.Results;

/// <summary> Represents one structured file diff payload for an installation operation action. </summary>
public sealed class SkillActionDiff
{
    /// <summary> Initializes one structured file diff payload. </summary>
    /// <param name="files"> The file-level differences. </param>
    internal SkillActionDiff (IReadOnlyList<SkillFileDiff> files)
    {
        Files = SkillActionContractGuard.Snapshot(files, nameof(files));
        if (Files.Count == 0)
        {
            throw new ArgumentException("A structured action diff must contain at least one file difference.", nameof(files));
        }

        if (Files.Select(static file => file.RelativePath).Distinct(StringComparer.Ordinal).Count() != Files.Count)
        {
            throw new ArgumentException("A structured action diff must not contain duplicate file paths.", nameof(files));
        }
    }

    /// <summary> Gets an immutable snapshot of the file-level differences. </summary>
    public IReadOnlyList<SkillFileDiff> Files { get; }
}

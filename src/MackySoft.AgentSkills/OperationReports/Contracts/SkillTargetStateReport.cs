namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents a target state with stable literals suitable for product CLI payloads. </summary>
/// <param name="Kind"> The stable target state literal. </param>
/// <param name="Code"> The failure code value represented by this state, when present. </param>
/// <param name="Message"> The failure message represented by this state, when present. </param>
/// <param name="InstalledSkillBundleVersion"> The installed target SKILL bundle version, when available. </param>
/// <param name="BundledSkillBundleVersion"> The bundled canonical package SKILL bundle version, when available. </param>
/// <param name="FileSet"> The structured file-set drift details, when this state represents file-set drift. </param>
public sealed record SkillTargetStateReport (
    string Kind,
    string? Code = null,
    string? Message = null,
    int? InstalledSkillBundleVersion = null,
    int? BundledSkillBundleVersion = null,
    SkillTargetFileSetReport? FileSet = null)
{
    /// <summary> Initializes a report using the pre-skillBundleVersion constructor shape. </summary>
    public SkillTargetStateReport (
        string Kind,
        string? Code,
        string? Message,
        SkillTargetFileSetReport? FileSet)
        : this(
            Kind,
            Code,
            Message,
            null,
            null,
            FileSet)
    {
    }

    /// <summary> Deconstructs a report using the pre-skillBundleVersion tuple shape. </summary>
    public void Deconstruct (
        out string Kind,
        out string? Code,
        out string? Message,
        out SkillTargetFileSetReport? FileSet)
    {
        Kind = this.Kind;
        Code = this.Code;
        Message = this.Message;
        FileSet = this.FileSet;
    }
}

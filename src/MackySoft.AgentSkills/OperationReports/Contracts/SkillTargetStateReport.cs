using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.OperationReports.Contracts;

/// <summary> Represents a target state suitable for product CLI payloads. </summary>
public sealed class SkillTargetStateReport
{
    /// <summary> Projects one validated action target state into report data. </summary>
    /// <param name="state"> The validated action target state. </param>
    internal SkillTargetStateReport (SkillActionTargetState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Kind = state.Kind;
        Code = state.Code?.Value;
        Message = state.Message;
        InstalledSkillBundleVersion = state.InstalledSkillBundleVersion;
        BundledSkillBundleVersion = state.BundledSkillBundleVersion;
        FileSet = state.FileSet is null
            ? null
            : new SkillTargetFileSetReport(
                state.FileSet.MissingFiles,
                state.FileSet.ExtraFiles,
                state.FileSet.ExtraDirectories);
    }

    /// <summary> Gets the target state kind. </summary>
    public SkillTargetStateKind Kind { get; }

    /// <summary> Gets the failure code value represented by this state, when present. </summary>
    public string? Code { get; }

    /// <summary> Gets the failure message represented by this state, when present. </summary>
    public string? Message { get; }

    /// <summary> Gets the installed target SKILL bundle version, when available. </summary>
    public int? InstalledSkillBundleVersion { get; }

    /// <summary> Gets the bundled canonical package SKILL bundle version, when available. </summary>
    public int? BundledSkillBundleVersion { get; }

    /// <summary> Gets structured file-set drift details when they were available for this state. </summary>
    public SkillTargetFileSetReport? FileSet { get; }

}

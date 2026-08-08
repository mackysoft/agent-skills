using MackySoft.AgentSkills.Doctor;

namespace MackySoft.AgentSkills.Agents.Doctor;

/// <summary> Represents custom-agent diagnostics and the distinct resolved-SKILL doctor result. </summary>
public sealed class AgentDoctorResult
{
    /// <summary> Initializes one immutable doctor result. </summary>
    internal AgentDoctorResult (string artifactRoot, string stateRoot, IReadOnlyList<AgentDoctorDiagnostic> diagnostics, SkillDoctorResult skillResult)
    {
        ArtifactRoot = Path.GetFullPath(artifactRoot);
        StateRoot = Path.GetFullPath(stateRoot);
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        SkillResult = skillResult ?? throw new ArgumentNullException(nameof(skillResult));
    }

    /// <summary> Gets the diagnosed host-discovered artifact root. </summary>
    public string ArtifactRoot { get; }

    /// <summary> Gets the diagnosed Agent Skills ownership-state root. </summary>
    public string StateRoot { get; }

    /// <summary> Gets custom-agent package, host-artifact, and target-state diagnostics. </summary>
    public IReadOnlyList<AgentDoctorDiagnostic> Diagnostics { get; }

    /// <summary> Gets the separate diagnostic result for resolved SKILL dependencies. </summary>
    public SkillDoctorResult SkillResult { get; }

    /// <summary> Gets whether both custom agents and resolved SKILL dependencies are healthy. </summary>
    public bool IsHealthy => Diagnostics.All(static diagnostic => !diagnostic.IsError) && SkillResult.IsHealthy;
}

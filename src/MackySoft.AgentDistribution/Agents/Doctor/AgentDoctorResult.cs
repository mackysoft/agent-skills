using MackySoft.AgentDistribution.Doctor;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Agents.Doctor;

/// <summary> Represents custom-agent diagnostics and the distinct resolved-SKILL doctor result. </summary>
public sealed class AgentDoctorResult
{
    /// <summary> Initializes one immutable doctor result. </summary>
    internal AgentDoctorResult (
        AbsolutePath artifactRoot,
        AbsolutePath stateRoot,
        IReadOnlyList<AgentDoctorDiagnostic> diagnostics,
        SkillDoctorResult skillResult)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var diagnosticSnapshot = diagnostics.ToArray();
        if (diagnosticSnapshot.Any(static diagnostic => diagnostic is null))
        {
            throw new ArgumentException("Agent doctor diagnostics must not contain null items.", nameof(diagnostics));
        }

        ArtifactRoot = artifactRoot ?? throw new ArgumentNullException(nameof(artifactRoot));
        StateRoot = stateRoot ?? throw new ArgumentNullException(nameof(stateRoot));
        Diagnostics = Array.AsReadOnly(diagnosticSnapshot);
        SkillResult = skillResult ?? throw new ArgumentNullException(nameof(skillResult));
    }

    /// <summary> Gets the diagnosed host-discovered artifact root. </summary>
    public AbsolutePath ArtifactRoot { get; }

    /// <summary> Gets the diagnosed Agent Distribution ownership-state root. </summary>
    public AbsolutePath StateRoot { get; }

    /// <summary> Gets custom-agent package, host-artifact, and target-state diagnostics. </summary>
    public IReadOnlyList<AgentDoctorDiagnostic> Diagnostics { get; }

    /// <summary> Gets the separate diagnostic result for resolved SKILL dependencies. </summary>
    public SkillDoctorResult SkillResult { get; }

    /// <summary> Gets whether both custom agents and resolved SKILL dependencies are healthy. </summary>
    public bool IsHealthy => Diagnostics.All(static diagnostic => !diagnostic.IsError) && SkillResult.IsHealthy;
}

using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Doctor;

/// <summary> Represents a SKILL doctor result. </summary>
public sealed class SkillDoctorResult
{
    /// <summary> Initializes one immutable doctor result. </summary>
    /// <param name="host"> The diagnosed host. </param>
    /// <param name="targetRoot"> The diagnosed bundle target root. </param>
    /// <param name="diagnostics"> The complete diagnostics. </param>
    public SkillDoctorResult (
        HostKind host,
        AbsolutePath targetRoot,
        IReadOnlyList<SkillDoctorDiagnostic> diagnostics)
    {
        if (!Vocabulary.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        ArgumentNullException.ThrowIfNull(diagnostics);
        var diagnosticSnapshot = diagnostics.ToArray();
        if (diagnosticSnapshot.Any(static diagnostic => diagnostic is null))
        {
            throw new ArgumentException("Doctor diagnostics must not contain null items.", nameof(diagnostics));
        }

        Host = host;
        TargetRoot = targetRoot ?? throw new ArgumentNullException(nameof(targetRoot));
        Diagnostics = Array.AsReadOnly(diagnosticSnapshot);
    }

    /// <summary> Gets the diagnosed host. </summary>
    public HostKind Host { get; }

    /// <summary> Gets the diagnosed bundle target root. </summary>
    public AbsolutePath TargetRoot { get; }

    /// <summary> Gets an immutable snapshot of diagnostics. </summary>
    public IReadOnlyList<SkillDoctorDiagnostic> Diagnostics { get; }

    /// <summary> Gets a value indicating whether no error diagnostics were reported. </summary>
    public bool IsHealthy => Diagnostics.All(static diagnostic => diagnostic.Severity != SkillDoctorSeverity.Error);
}

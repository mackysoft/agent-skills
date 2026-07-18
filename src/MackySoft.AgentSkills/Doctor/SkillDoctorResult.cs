using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Doctor;

/// <summary> Represents a SKILL doctor result. </summary>
public sealed class SkillDoctorResult
{
    /// <summary> Initializes one immutable doctor result. </summary>
    /// <param name="host"> The diagnosed host. </param>
    /// <param name="targetRoot"> The diagnosed bundle target root. </param>
    /// <param name="diagnostics"> The complete diagnostics. </param>
    public SkillDoctorResult (
        SkillHostKind host,
        string targetRoot,
        IReadOnlyList<SkillDoctorDiagnostic> diagnostics)
    {
        if (!ContractLiteralCodec.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        if (!Path.IsPathFullyQualified(targetRoot))
        {
            throw new ArgumentException("Doctor target root must be an absolute path.", nameof(targetRoot));
        }

        ArgumentNullException.ThrowIfNull(diagnostics);
        var diagnosticSnapshot = diagnostics.ToArray();
        if (diagnosticSnapshot.Any(static diagnostic => diagnostic is null))
        {
            throw new ArgumentException("Doctor diagnostics must not contain null items.", nameof(diagnostics));
        }

        Host = host;
        TargetRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetRoot));
        Diagnostics = Array.AsReadOnly(diagnosticSnapshot);
    }

    /// <summary> Gets the diagnosed host. </summary>
    public SkillHostKind Host { get; }

    /// <summary> Gets the diagnosed bundle target root. </summary>
    public string TargetRoot { get; }

    /// <summary> Gets an immutable snapshot of diagnostics. </summary>
    public IReadOnlyList<SkillDoctorDiagnostic> Diagnostics { get; }

    /// <summary> Gets a value indicating whether no error diagnostics were reported. </summary>
    public bool IsHealthy => Diagnostics.All(static diagnostic => diagnostic.Severity != SkillDoctorSeverity.Error);
}

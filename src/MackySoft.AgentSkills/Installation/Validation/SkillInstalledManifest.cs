using MackySoft.AgentSkills.Manifests;

namespace MackySoft.AgentSkills.Installation.Validation;

/// <summary> Represents a validated installed SKILL manifest and its source text. </summary>
public sealed class SkillInstalledManifest
{
    /// <summary> Initializes one installed manifest produced by the installed manifest reader. </summary>
    /// <param name="manifestPath"> The installed manifest file path. </param>
    /// <param name="manifestText"> The installed manifest JSON text. </param>
    /// <param name="manifest"> The validated manifest model. </param>
    internal SkillInstalledManifest (
        string manifestPath,
        string manifestText,
        SkillManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        if (!Path.IsPathFullyQualified(manifestPath))
        {
            throw new ArgumentException("Installed manifest path must be absolute.", nameof(manifestPath));
        }

        if (!string.Equals(Path.GetFileName(manifestPath), "agent-skill.json", StringComparison.Ordinal))
        {
            throw new ArgumentException("Installed manifest path must identify agent-skill.json.", nameof(manifestPath));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(manifestText);

        ManifestPath = Path.GetFullPath(manifestPath);
        ManifestText = manifestText;
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
    }

    /// <summary> Gets the installed manifest file path. </summary>
    public string ManifestPath { get; }

    /// <summary> Gets the installed manifest JSON text. </summary>
    public string ManifestText { get; }

    /// <summary> Gets the validated manifest model. </summary>
    public SkillManifest Manifest { get; }
}

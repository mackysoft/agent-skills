using MackySoft.AgentDistribution.Manifests;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Validation;

/// <summary> Represents a validated installed SKILL manifest and its source text. </summary>
public sealed class SkillInstalledManifest
{
    /// <summary> Initializes one installed manifest produced by the installed manifest reader. </summary>
    /// <param name="manifestPath"> The installed manifest file path. </param>
    /// <param name="manifestText"> The installed manifest JSON text. </param>
    /// <param name="manifest"> The validated manifest model. </param>
    internal SkillInstalledManifest (
        AbsolutePath manifestPath,
        string manifestText,
        SkillManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifestPath);

        if (!string.Equals(Path.GetFileName(manifestPath.Value), "agent-skill.json", StringComparison.Ordinal))
        {
            throw new ArgumentException("Installed manifest path must identify agent-skill.json.", nameof(manifestPath));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(manifestText);

        ManifestPath = manifestPath;
        ManifestText = manifestText;
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
    }

    /// <summary> Gets the installed manifest file path. </summary>
    public AbsolutePath ManifestPath { get; }

    /// <summary> Gets the installed manifest JSON text. </summary>
    public string ManifestText { get; }

    /// <summary> Gets the validated manifest model. </summary>
    public SkillManifest Manifest { get; }
}

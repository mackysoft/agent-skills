using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Manifests;

namespace MackySoft.AgentSkills.Installation.Inventory;

/// <summary> Represents one scanned installed SKILL manifest. </summary>
public sealed class SkillInstalledSkill
{
    /// <summary> Initializes one installed SKILL produced by the installation scanner. </summary>
    /// <param name="identity"> The install identity. </param>
    /// <param name="skillDirectory"> The skill directory. </param>
    /// <param name="manifest"> The scanned manifest. </param>
    internal SkillInstalledSkill (
        SkillInstallIdentity identity,
        string skillDirectory,
        SkillManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        if (!Path.IsPathFullyQualified(skillDirectory))
        {
            throw new ArgumentException("Installed SKILL directory must be an absolute path.", nameof(skillDirectory));
        }

        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.SkillName != identity.SkillName)
        {
            throw new ArgumentException("Installed manifest SKILL name must match the install identity.", nameof(manifest));
        }

        var canonicalSkillDirectory = Path.GetFullPath(skillDirectory);
        var expectedSkillDirectory = Path.GetFullPath(Path.Combine(identity.TargetRoot, identity.SkillName.Value));
        var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(
                Path.TrimEndingDirectorySeparator(canonicalSkillDirectory),
                Path.TrimEndingDirectorySeparator(expectedSkillDirectory),
                pathComparison))
        {
            throw new ArgumentException("Installed SKILL directory must match the install identity.", nameof(skillDirectory));
        }

        Identity = identity;
        SkillDirectory = canonicalSkillDirectory;
        Manifest = manifest;
    }

    /// <summary> Gets the install identity. </summary>
    public SkillInstallIdentity Identity { get; }

    /// <summary> Gets the canonical absolute skill directory. </summary>
    public string SkillDirectory { get; }

    /// <summary> Gets the scanned manifest. </summary>
    public SkillManifest Manifest { get; }
}

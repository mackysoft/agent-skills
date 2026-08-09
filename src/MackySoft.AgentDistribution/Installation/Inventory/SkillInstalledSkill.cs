using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Manifests;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Inventory;

/// <summary> Represents one scanned installed SKILL manifest. </summary>
public sealed class SkillInstalledSkill
{
    /// <summary> Initializes one installed SKILL produced by the installation scanner. </summary>
    /// <param name="identity"> The install identity. </param>
    /// <param name="skillDirectory"> The skill directory. </param>
    /// <param name="manifest"> The scanned manifest. </param>
    internal SkillInstalledSkill (
        SkillInstallIdentity identity,
        AbsolutePath skillDirectory,
        SkillManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(skillDirectory);

        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.SkillName != identity.SkillName)
        {
            throw new ArgumentException("Installed manifest SKILL name must match the install identity.", nameof(manifest));
        }

        var expectedSkillDirectory = ContainedPath.Create(
            identity.TargetRoot,
            RootRelativePath.Parse(identity.SkillName.Value)).Target;
        if (!skillDirectory.IsSameAs(expectedSkillDirectory))
        {
            throw new ArgumentException("Installed SKILL directory must match the install identity.", nameof(skillDirectory));
        }

        Identity = identity;
        SkillDirectory = skillDirectory;
        Manifest = manifest;
    }

    /// <summary> Gets the install identity. </summary>
    public SkillInstallIdentity Identity { get; }

    /// <summary> Gets the canonical absolute skill directory. </summary>
    public AbsolutePath SkillDirectory { get; }

    /// <summary> Gets the scanned manifest. </summary>
    public SkillManifest Manifest { get; }
}

using MackySoft.AgentSkills.Bundles;
using MackySoft.AgentSkills.Sources;

namespace MackySoft.AgentSkills.Generation;

/// <summary> Holds one validated source snapshot used to generate all versions of the same bundle content. </summary>
internal sealed class SkillPackageGenerationSource
{
    /// <summary> Initializes one validated source snapshot. </summary>
    /// <param name="bundleDefinition"> The authored bundle identity and version read with the source definitions. </param>
    /// <param name="definitions"> The non-empty source definition set. </param>
    internal SkillPackageGenerationSource (
        SkillBundleDefinition bundleDefinition,
        IReadOnlyList<SkillSourceDefinition> definitions)
    {
        BundleDefinition = bundleDefinition ?? throw new ArgumentNullException(nameof(bundleDefinition));
        ArgumentNullException.ThrowIfNull(definitions);
        if (definitions.Count == 0)
        {
            throw new ArgumentException("SKILL package generation source must contain at least one definition.", nameof(definitions));
        }

        if (definitions.Any(static definition => definition is null))
        {
            throw new ArgumentException("SKILL package generation source must not contain null definitions.", nameof(definitions));
        }

        Definitions = Array.AsReadOnly(definitions.ToArray());
    }

    /// <summary> Gets the authored bundle definition read with this snapshot. </summary>
    internal SkillBundleDefinition BundleDefinition { get; }

    /// <summary> Gets an immutable snapshot of source definitions. </summary>
    internal IReadOnlyList<SkillSourceDefinition> Definitions { get; }
}

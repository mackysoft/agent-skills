namespace MackySoft.AgentSkills.Bundles;

/// <summary> Describes the canonical bundle state established by one build operation. </summary>
public sealed class SkillBundleBuildResult
{
    /// <summary> Initializes one successful build result. </summary>
    /// <param name="changed"> Whether the operation published generated or source files. </param>
    /// <param name="descriptor"> The descriptor matching the resulting canonical bundle state. </param>
    internal SkillBundleBuildResult (
        bool changed,
        SkillBundleDescriptor descriptor)
    {
        Changed = changed;
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    /// <summary> Gets whether the operation published generated or source files. </summary>
    public bool Changed { get; }

    /// <summary> Gets the descriptor matching the resulting canonical bundle state. </summary>
    public SkillBundleDescriptor Descriptor { get; }
}

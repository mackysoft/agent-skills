namespace MackySoft.AgentSkills.Bundles;

/// <summary> Describes one successful v2 build operation. </summary>
public sealed class AgentSkillsBundleBuildResult
{
    /// <summary> Initializes the result. </summary>
    internal AgentSkillsBundleBuildResult (bool changed, AgentSkillsBundleDescriptor descriptor)
    {
        Changed = changed;
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    /// <summary> Gets whether files changed. </summary>
    public bool Changed { get; }
    /// <summary> Gets resulting descriptor. </summary>
    public AgentSkillsBundleDescriptor Descriptor { get; }
}

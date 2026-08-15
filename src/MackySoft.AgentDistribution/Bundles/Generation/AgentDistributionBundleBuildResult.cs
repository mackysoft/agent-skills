namespace MackySoft.AgentDistribution.Bundles.Generation;

/// <summary> Describes one successful v3 build operation. </summary>
public sealed class AgentDistributionBundleBuildResult
{
    /// <summary> Initializes the result. </summary>
    internal AgentDistributionBundleBuildResult (bool changed, AgentDistributionBundleDescriptor descriptor)
    {
        Changed = changed;
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    /// <summary> Gets whether files changed. </summary>
    public bool Changed { get; }
    /// <summary> Gets resulting descriptor. </summary>
    public AgentDistributionBundleDescriptor Descriptor { get; }
}

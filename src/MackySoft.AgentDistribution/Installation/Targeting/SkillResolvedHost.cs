namespace MackySoft.AgentDistribution.Installation.Targeting;

/// <summary> Represents one supported host resolved for SKILL targeting and reporting. </summary>
public sealed class SkillResolvedHost
{
    /// <summary> Initializes one resolved host. </summary>
    internal SkillResolvedHost (HostKind host, SkillHostDescriptor descriptor)
    {
        if (!Vocabulary.IsDefined(host))
        {
            throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
        }

        Host = host;
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    /// <summary> Gets the supported host. </summary>
    public HostKind Host { get; }

    /// <summary> Gets the resolved host SKILL targeting and materialization descriptor. </summary>
    public SkillHostDescriptor Descriptor { get; }

    /// <summary> Gets host-specific guidance for reloading installed SKILLs. </summary>
    public string ReloadGuidance => Descriptor.ReloadGuidance;
}

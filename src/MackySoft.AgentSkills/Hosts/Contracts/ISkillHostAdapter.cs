namespace MackySoft.AgentSkills.Hosts.Contracts;

/// <summary> Provides deterministic materialization policy for one supported SKILL host. </summary>
internal interface ISkillHostAdapter
{
    /// <summary> Gets the host descriptor. </summary>
    SkillHostDescriptor Descriptor { get; }

    /// <summary> Builds host-specific artifacts for one skill. </summary>
    /// <param name="metadata"> The host-independent metadata. </param>
    /// <returns> The host-specific artifact set. </returns>
    SkillHostArtifactSet BuildArtifacts (SkillHostMetadata metadata);
}

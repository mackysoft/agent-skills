using MackySoft.AgentSkills.Agents.Hosts;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Hosts.Registration;

/// <summary> Provides the explicitly registered custom-agent host adapters. </summary>
public sealed class AgentHostAdapterSet
{
    private readonly IReadOnlyDictionary<AgentHostKind, IAgentHostAdapter> adapters;

    /// <summary> Initializes the default adapter set. </summary>
    public AgentHostAdapterSet () : this([new OpenAiAgentHostAdapter()])
    {
    }

    /// <summary> Initializes a deterministic adapter set. </summary>
    internal AgentHostAdapterSet (IReadOnlyList<IAgentHostAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        if (adapters.Any(static adapter => adapter is null)
            || adapters.Any(static adapter => adapter.Descriptor.HostId != adapter.HostId)
            || adapters.GroupBy(static adapter => adapter.HostId).Any(static group => group.Count() != 1))
        {
            throw new ArgumentException("Agent host adapters must be non-null, descriptor-consistent, and have unique host IDs.", nameof(adapters));
        }

        this.adapters = adapters.ToDictionary(static adapter => adapter.HostId);
    }

    /// <summary> Gets an adapter or a host-unsupported failure. </summary>
    internal SkillOperationResult<IAgentHostAdapter> GetAdapter (AgentHostKind hostId)
    {
        if (!Vocabulary.IsDefined(hostId))
        {
            throw new ArgumentOutOfRangeException(nameof(hostId), hostId, "Unsupported agent host.");
        }

        return adapters.TryGetValue(hostId, out var adapter)
            ? SkillOperationResult<IAgentHostAdapter>.Success(adapter)
            : SkillOperationResult<IAgentHostAdapter>.FailureResult(SkillFailureCodes.HostUnsupported, $"Unsupported agent host binding: {Vocabulary.GetText(hostId)}");
    }
}

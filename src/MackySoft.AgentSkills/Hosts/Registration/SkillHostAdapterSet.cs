using MackySoft.AgentSkills.Hosts.Claude;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Copilot;
using MackySoft.AgentSkills.Hosts.OpenAi;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Hosts.Registration;

/// <summary> Provides the deterministic global host adapter set. </summary>
public sealed class SkillHostAdapterSet
{
    private static readonly IReadOnlyList<ISkillHostAdapter> SupportedAdapters = Array.AsReadOnly<ISkillHostAdapter>(
        [
            new ClaudeSkillHostAdapter(),
            new CopilotSkillHostAdapter(),
            new OpenAiSkillHostAdapter(),
        ]);

    /// <summary> Initializes a new instance of the <see cref="SkillHostAdapterSet" /> class. </summary>
    public SkillHostAdapterSet ()
    {
    }

    /// <summary> Gets all supported host adapters in deterministic order. </summary>
    internal IReadOnlyList<ISkillHostAdapter> Adapters => SupportedAdapters;

    /// <summary> Gets one adapter by host. </summary>
    /// <param name="host"> The supported host. </param>
    /// <returns> The adapter or unsupported-host failure. </returns>
    internal SkillOperationResult<ISkillHostAdapter> GetAdapter (SkillHostKind host)
    {
        if (!ContractLiteralCodec.IsDefined(host))
        {
            return SkillOperationResult<ISkillHostAdapter>.FailureResult(
                SkillFailureCodes.HostUnsupported,
                $"Unsupported SKILL host value: {host}");
        }

        foreach (var adapter in SupportedAdapters)
        {
            if (adapter.Descriptor.Host == host)
            {
                return SkillOperationResult<ISkillHostAdapter>.Success(adapter);
            }
        }

        return SkillOperationResult<ISkillHostAdapter>.FailureResult(
            SkillFailureCodes.HostUnsupported,
            $"Unsupported SKILL host: {ContractLiteralCodec.ToValue(host)}");
    }

}

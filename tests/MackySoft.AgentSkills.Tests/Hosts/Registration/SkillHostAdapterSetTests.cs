using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Hosts.Registration;

public sealed class SkillHostAdapterSetTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_ProvidesCompleteSupportedHostSetInContractOrder ()
    {
        var adapterSet = new SkillHostAdapterSet();

        Assert.Equal(
            [SkillHostKind.Claude, SkillHostKind.Copilot, SkillHostKind.OpenAi],
            adapterSet.Adapters.Select(static adapter => adapter.Descriptor.Host));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetAdapter_ReturnsUnsupportedHostFailure_ForUndefinedHostValue ()
    {
        var result = new SkillHostAdapterSet().GetAdapter((SkillHostKind)42);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }
}

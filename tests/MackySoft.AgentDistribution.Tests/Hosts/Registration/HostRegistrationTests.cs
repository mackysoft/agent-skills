using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Hosts.Registration;

public sealed class HostRegistrationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Get_ReturnsUnsupportedHostFailure_ForUndefinedHostValue ()
    {
        var result = HostRegistration.Get((HostKind)42);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }
}

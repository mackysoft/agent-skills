using MackySoft.AgentDistribution.Hosts.Registration;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Hosts.Registration;

public sealed class HostRegistrationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Registrations_ContainsEveryHostExactlyOnce ()
    {
        var registeredHosts = BuiltInHostCatalog.Registrations
            .Select(static registration => registration.Host)
            .Order()
            .ToArray();

        Assert.Equal(Enum.GetValues<HostKind>().Order(), registeredHosts);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Get_ReturnsUnsupportedHostFailure_ForUndefinedHostValue ()
    {
        var result = BuiltInHostCatalog.Get((HostKind)42);

        Assert.False(result.IsSuccess);
        Assert.Equal(AgentDistributionFailureCodes.HostUnsupported, result.Failure!.Code);
    }
}

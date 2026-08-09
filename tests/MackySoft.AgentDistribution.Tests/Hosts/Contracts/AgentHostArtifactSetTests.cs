using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Hosts.Contracts;

public sealed class AgentHostArtifactSetTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsCaseOnlyArtifactPathCollisions ()
    {
        Assert.Throws<ArgumentException>(() => new AgentHostArtifactSet(
        [
            new AgentHostArtifactFile(PackageRelativePath.Parse("agent.md"), "first"),
            new AgentHostArtifactFile(PackageRelativePath.Parse("AGENT.md"), "second"),
        ]));
    }
}

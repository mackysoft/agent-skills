using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Hosts.Contracts;

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

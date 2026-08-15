namespace MackySoft.AgentDistribution.Tests.Hosts.Contracts;

public sealed class AgentHostArtifactRequestTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsInstructionsThatAreNotLfNormalized ()
    {
        Assert.Throws<ArgumentException>(() => new AgentHostArtifactRequest(
            new AgentName("architect"),
            "Creates an implementation-ready design contract.",
            "Line 1\r\nLine 2\n",
            "{\"schemaVersion\":1}"));
    }
}

using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Shared;

public sealed class AgentDistributionFailureCodeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RetainsUnknownCodeValue ()
    {
        AgentDistributionFailureCode code = new("AGENT_DISTRIBUTION_FUTURE_FAILURE");

        Assert.Equal("AGENT_DISTRIBUTION_FUTURE_FAILURE", code.Value);
        Assert.Equal("AGENT_DISTRIBUTION_FUTURE_FAILURE", code.ToString());
        string rawValue = code;
        Assert.Equal("AGENT_DISTRIBUTION_FUTURE_FAILURE", rawValue);
        Assert.Equal(new AgentDistributionFailureCode("AGENT_DISTRIBUTION_FUTURE_FAILURE"), code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_RejectsBlankValue (string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new AgentDistributionFailureCode(value!));
        Assert.False(AgentDistributionFailureCode.TryCreate(value, out var code));
        Assert.Null(code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_ReturnsValidatedCode ()
    {
        var result = AgentDistributionFailureCode.TryCreate("AGENT_DISTRIBUTION_VALID", out var code);

        Assert.True(result);
        Assert.Equal(new AgentDistributionFailureCode("AGENT_DISTRIBUTION_VALID"), code);
    }
}

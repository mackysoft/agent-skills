using MackySoft.AgentDistribution.Hosting.Commands;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Hosting;

public sealed class AgentDistributionCommandResultTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Success_CreatesPayloadResult ()
    {
        var payload = new object();

        var result = AgentDistributionCommandResult.Success("test.command", payload);

        Assert.True(result.IsSuccess);
        Assert.Equal("test.command", result.Command);
        Assert.Same(payload, result.Payload);
        Assert.Null(result.Failure);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FailureResult_CreatesFailureResult ()
    {
        var failure = AgentDistributionFailure.Create(AgentDistributionFailureCodes.InputInvalid, "Invalid input.");

        var result = AgentDistributionCommandResult.FailureResult("test.command", failure);

        Assert.False(result.IsSuccess);
        Assert.Equal("test.command", result.Command);
        Assert.Null(result.Payload);
        Assert.Same(failure, result.Failure);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Success_WhenPayloadIsNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => AgentDistributionCommandResult.Success("test.command", null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FailureResult_WhenFailureIsNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => AgentDistributionCommandResult.FailureResult("test.command", null!));
    }
}

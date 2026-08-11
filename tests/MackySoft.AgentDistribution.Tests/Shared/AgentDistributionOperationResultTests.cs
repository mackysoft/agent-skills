using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Shared;

public sealed class AgentDistributionOperationResultTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Success_CreatesSuccessfulStateOnly ()
    {
        var value = new object();

        var result = AgentDistributionOperationResult<object>.Success(value);

        Assert.True(result.IsSuccess);
        Assert.Same(value, result.Value);
        Assert.Null(result.Failure);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FailureResult_CreatesFailureStateOnly ()
    {
        var result = AgentDistributionOperationResult<object>.FailureResult(AgentDistributionFailureCodes.InputInvalid, "Invalid input.");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(AgentDistributionFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Equal("Invalid input.", result.Failure.Message);
    }
}

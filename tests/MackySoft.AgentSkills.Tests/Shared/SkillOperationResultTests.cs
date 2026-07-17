using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Shared;

public sealed class SkillOperationResultTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Success_CreatesSuccessfulStateOnly ()
    {
        var value = new object();

        var result = SkillOperationResult<object>.Success(value);

        Assert.True(result.IsSuccess);
        Assert.Same(value, result.Value);
        Assert.Null(result.Failure);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FailureResult_CreatesFailureStateOnly ()
    {
        var result = SkillOperationResult<object>.FailureResult(SkillFailureCodes.InputInvalid, "Invalid input.");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(SkillFailureCodes.InputInvalid, result.Failure!.Code);
        Assert.Equal("Invalid input.", result.Failure.Message);
    }
}

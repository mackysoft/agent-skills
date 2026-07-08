using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Hosting;

public sealed class AgentSkillsCommandResultTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Success_CreatesPayloadResult ()
    {
        var payload = new object();

        var result = AgentSkillsCommandResult.Success("skills.list", payload);

        Assert.True(result.IsSuccess);
        Assert.Equal("skills.list", result.Command);
        Assert.Same(payload, result.Payload);
        Assert.Null(result.Failure);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FailureResult_CreatesFailureResult ()
    {
        var failure = SkillFailure.Create(SkillFailureCodes.InputInvalid, "Invalid input.");

        var result = AgentSkillsCommandResult.FailureResult("skills.install", failure);

        Assert.False(result.IsSuccess);
        Assert.Equal("skills.install", result.Command);
        Assert.Null(result.Payload);
        Assert.Same(failure, result.Failure);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Success_WhenPayloadIsNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => AgentSkillsCommandResult.Success("skills.list", null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FailureResult_WhenFailureIsNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => AgentSkillsCommandResult.FailureResult("skills.install", null!));
    }
}

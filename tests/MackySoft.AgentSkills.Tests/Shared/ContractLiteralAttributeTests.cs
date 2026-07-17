using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Tests.Shared;

public sealed class ContractLiteralAttributeTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" value")]
    [InlineData("value ")]
    public void Constructor_RejectsEmptyOrOuterWhitespaceLiteral (string literal)
    {
        Assert.Throws<ArgumentException>(() => new ContractLiteralAttribute(literal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsNullLiteral ()
    {
        Assert.Throws<ArgumentNullException>(() => new ContractLiteralAttribute(null!));
    }
}

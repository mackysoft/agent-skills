using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Tests.Shared;

public sealed class ContractLiteralCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ToValue_ReturnsCanonicalLiteral ()
    {
        Assert.Equal("second-value", ContractLiteralCodec.ToValue(SampleLiteral.Second));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_ParsesCanonicalLiteralExactly ()
    {
        Assert.True(ContractLiteralCodec.TryParse("firstValue", out SampleLiteral value));
        Assert.Equal(SampleLiteral.First, value);
        Assert.False(ContractLiteralCodec.TryParse("FIRSTVALUE", out value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetLiterals_ReturnsLiteralsInDeclarationOrder ()
    {
        Assert.Equal(["firstValue", "second-value"], ContractLiteralCodec.GetLiterals<SampleLiteral>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToValue_RejectsUndefinedEnumValue ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ContractLiteralCodec.ToValue((SampleLiteral)999));
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidContractLiteralAccessors))]
    public void GetLiterals_RejectsInvalidEnumContracts (Action access)
    {
        Assert.Throws<InvalidOperationException>(access);
    }

    public static TheoryData<Action> InvalidContractLiteralAccessors =>
    [
        static () => ContractLiteralCodec.GetLiterals<MissingLiteral>(),
        static () => ContractLiteralCodec.GetLiterals<DuplicateLiteral>(),
        static () => ContractLiteralCodec.GetLiterals<DuplicateValue>(),
        static () => ContractLiteralCodec.GetLiterals<WhitespaceLiteral>(),
    ];

    private enum SampleLiteral
    {
        [ContractLiteral("firstValue")]
        First = 0,

        [ContractLiteral("second-value")]
        Second = 1,
    }

    private enum MissingLiteral
    {
        Value = 0,
    }

    private enum DuplicateLiteral
    {
        [ContractLiteral("same")]
        First = 0,

        [ContractLiteral("same")]
        Second = 1,
    }

    private enum DuplicateValue
    {
        [ContractLiteral("first")]
        First = 0,

        [ContractLiteral("second")]
        Second = 0,
    }

    private enum WhitespaceLiteral
    {
        [ContractLiteral(" value")]
        Value = 0,
    }
}

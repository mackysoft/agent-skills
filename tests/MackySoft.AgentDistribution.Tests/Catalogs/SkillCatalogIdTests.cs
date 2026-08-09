using MackySoft.AgentDistribution.Catalogs;

namespace MackySoft.AgentDistribution.Tests.Catalogs;

public sealed class SkillCatalogIdTests
{
    [Theory]
    [InlineData("com.mackysoft.agent-distribution")]
    [InlineData("com.mackysoft.skills-pack")]
    [InlineData("dev1.tools")]
    [Trait("Size", "Small")]
    public void TryCreate_AcceptsSafeCatalogIds (string value)
    {
        var result = SkillCatalogId.TryCreate(value, out var catalogId);

        Assert.True(result);
        Assert.Equal(value, catalogId!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Com.MackySoft.AgentDistribution")]
    [InlineData("com..mackysoft")]
    [InlineData("com.mackysoft.")]
    [InlineData("com.-mackysoft")]
    [InlineData("com.mackysoft-")]
    [InlineData("com_mackysoft")]
    [Trait("Size", "Small")]
    public void TryCreate_RejectsUnsafeCatalogIds (string? value)
    {
        var result = SkillCatalogId.TryCreate(value, out var catalogId);

        Assert.False(result);
        Assert.Null(catalogId);
    }
}

using System.Text.Json;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Tests.Shared;

public sealed class ContractLiteralJsonConverterFactoryTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters =
        {
            new ContractLiteralJsonConverterFactory(),
        },
    };

    [Fact]
    [Trait("Size", "Small")]
    public void SerializeAndDeserialize_UseCanonicalLiteral ()
    {
        var json = JsonSerializer.Serialize(SkillScopeKind.Project, Options);
        var value = JsonSerializer.Deserialize<SkillScopeKind>(json, Options);

        Assert.Equal("\"project\"", json);
        Assert.Equal(SkillScopeKind.Project, value);
    }

    [Theory]
    [InlineData("\"PROJECT\"")]
    [InlineData("\" project \"")]
    [InlineData("\"unsupported\"")]
    [InlineData("0")]
    [Trait("Size", "Small")]
    public void Deserialize_RejectsNonCanonicalValue (string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SkillScopeKind>(json, Options));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DictionaryKeys_UseCanonicalLiterals ()
    {
        var json = JsonSerializer.Serialize(
            new Dictionary<SkillScopeKind, int> { [SkillScopeKind.User] = 1 },
            Options);
        var values = JsonSerializer.Deserialize<Dictionary<SkillScopeKind, int>>(json, Options);

        Assert.Equal("{\"user\":1}", json);
        Assert.NotNull(values);
        Assert.Equal(1, values[SkillScopeKind.User]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SerializeDictionary_RejectsUndefinedEnumKey ()
    {
        var values = new Dictionary<SkillScopeKind, int>
        {
            [(SkillScopeKind)999] = 1,
        };

        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(values, Options));
    }

    [Theory]
    [InlineData("{\"USER\":1}")]
    [InlineData("{\" user \":1}")]
    [InlineData("{\"unsupported\":1}")]
    [Trait("Size", "Small")]
    public void DeserializeDictionary_RejectsNonCanonicalPropertyName (string json)
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Dictionary<SkillScopeKind, int>>(json, Options));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CanConvert_ReturnsFalseForEnumWithoutContractLiterals ()
    {
        var converter = new ContractLiteralJsonConverterFactory();

        Assert.False(converter.CanConvert(typeof(PlainEnum)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CanConvert_RejectsPartiallyMappedEnum ()
    {
        var converter = new ContractLiteralJsonConverterFactory();

        Assert.Throws<InvalidOperationException>(() => converter.CanConvert(typeof(PartiallyMappedEnum)));
    }

    private enum PartiallyMappedEnum
    {
        [ContractLiteral("mapped")]
        Mapped = 0,

        Unmapped = 1,
    }

    private enum PlainEnum
    {
        Value = 0,
    }
}

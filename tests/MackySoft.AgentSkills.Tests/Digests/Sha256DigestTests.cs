using System.Text;
using System.Text.Json;
using MackySoft.AgentSkills.Digests;

namespace MackySoft.AgentSkills.Tests.Digests;

public sealed class Sha256DigestTests
{
    private const string AbcDigest = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    [Trait("Size", "Small")]
    public void Compute_ReturnsDigestValueForKnownInput ()
    {
        var digest = Sha256Digest.Compute(Encoding.UTF8.GetBytes("abc"));

        Assert.Equal(AbcDigest, digest.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_ReturnsEqualValuesForSameCanonicalText ()
    {
        var first = Sha256Digest.Parse(AbcDigest);
        var second = Sha256Digest.Parse(AbcDigest);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015a")]
    [InlineData("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD")]
    [InlineData("za7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    public void TryParse_ReturnsFalseForNonCanonicalText (string? value)
    {
        var parsed = Sha256Digest.TryParse(value, out var digest);

        Assert.False(parsed);
        Assert.Null(digest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_ThrowsFormatExceptionForNonCanonicalText ()
    {
        Assert.Throws<FormatException>(() => Sha256Digest.Parse("not-a-digest"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonSerialization_RoundTripsAsCanonicalString ()
    {
        var digest = Sha256Digest.Parse(AbcDigest);

        var json = JsonSerializer.Serialize(digest);
        var roundTrip = JsonSerializer.Deserialize<Sha256Digest>(json);

        Assert.Equal($"\"{AbcDigest}\"", json);
        Assert.Equal(digest, roundTrip);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("123")]
    [InlineData("\"not-a-digest\"")]
    public void JsonDeserialization_ThrowsJsonExceptionForInvalidValue (string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Sha256Digest>(json));
    }
}

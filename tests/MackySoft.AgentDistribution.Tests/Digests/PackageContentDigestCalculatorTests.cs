using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Digests;

public sealed class PackageContentDigestCalculatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ComputeDigest_IsIndependentOfInputOrder ()
    {
        var calculator = new PackageContentDigestCalculator();

        var first = calculator.ComputeDigest(
        [
            new PackageContentDigestInputFile(PackageRelativePath.Parse("b.md"), "second\n"),
            new PackageContentDigestInputFile(PackageRelativePath.Parse("a.md"), "first\n"),
        ]);
        var second = calculator.ComputeDigest(
        [
            new PackageContentDigestInputFile(PackageRelativePath.Parse("a.md"), "first\n"),
            new PackageContentDigestInputFile(PackageRelativePath.Parse("b.md"), "second\n"),
        ]);

        Assert.Equal(first, second);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ComputeDigest_NormalizesLineEndings ()
    {
        var calculator = new PackageContentDigestCalculator();

        var lf = calculator.ComputeDigest([new PackageContentDigestInputFile(PackageRelativePath.Parse("SKILL.md"), "line1\nline2\n")]);
        var crlf = calculator.ComputeDigest([new PackageContentDigestInputFile(PackageRelativePath.Parse("SKILL.md"), "line1\r\nline2\r\n")]);

        Assert.Equal(lf, crlf);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ComputeDigest_SeparatesPathAndContentWithNullByte ()
    {
        var calculator = new PackageContentDigestCalculator();

        var first = calculator.ComputeDigest([new PackageContentDigestInputFile(PackageRelativePath.Parse("ab"), "c")]);
        var second = calculator.ComputeDigest([new PackageContentDigestInputFile(PackageRelativePath.Parse("a"), "bc")]);

        Assert.NotEqual(first, second);
    }
}

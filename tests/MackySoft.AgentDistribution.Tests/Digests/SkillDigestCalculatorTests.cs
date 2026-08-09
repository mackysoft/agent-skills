using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Digests;

public sealed class SkillDigestCalculatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ComputeDigest_IsIndependentOfInputOrder ()
    {
        var calculator = new SkillDigestCalculator();

        var first = calculator.ComputeDigest(
        [
            new SkillDigestInputFile(PackageRelativePath.Parse("b.md"), "second\n"),
            new SkillDigestInputFile(PackageRelativePath.Parse("a.md"), "first\n"),
        ]);
        var second = calculator.ComputeDigest(
        [
            new SkillDigestInputFile(PackageRelativePath.Parse("a.md"), "first\n"),
            new SkillDigestInputFile(PackageRelativePath.Parse("b.md"), "second\n"),
        ]);

        Assert.Equal(first, second);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ComputeDigest_NormalizesLineEndings ()
    {
        var calculator = new SkillDigestCalculator();

        var lf = calculator.ComputeDigest([new SkillDigestInputFile(PackageRelativePath.Parse("SKILL.md"), "line1\nline2\n")]);
        var crlf = calculator.ComputeDigest([new SkillDigestInputFile(PackageRelativePath.Parse("SKILL.md"), "line1\r\nline2\r\n")]);

        Assert.Equal(lf, crlf);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ComputeDigest_SeparatesPathAndContentWithNullByte ()
    {
        var calculator = new SkillDigestCalculator();

        var first = calculator.ComputeDigest([new SkillDigestInputFile(PackageRelativePath.Parse("ab"), "c")]);
        var second = calculator.ComputeDigest([new SkillDigestInputFile(PackageRelativePath.Parse("a"), "bc")]);

        Assert.NotEqual(first, second);
    }
}

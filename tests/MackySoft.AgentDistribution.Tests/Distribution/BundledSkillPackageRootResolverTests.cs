using MackySoft.AgentDistribution.Distribution;
using MackySoft.Tests;

namespace MackySoft.AgentDistribution.Tests.Distribution;

public sealed class BundledSkillPackageRootResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_ReturnsSkillsDirectoryDirectlyUnderBaseDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "bundled-root");
        var baseDirectory = scope.CreateDirectory("app");
        var skillsDirectory = AbsolutePath.Parse(scope.CreateDirectory("app/skills"));
        var resolver = new BundledSkillPackageRootResolver(AbsolutePath.Parse(baseDirectory));

        var result = resolver.Resolve();

        Assert.Equal(skillsDirectory, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_DoesNotWalkUpToParentSkillsDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "bundled-root-parent");
        var baseDirectory = scope.CreateDirectory("app");
        scope.CreateDirectory("skills");
        var resolver = new BundledSkillPackageRootResolver(AbsolutePath.Parse(baseDirectory));

        var exception = Assert.Throws<DirectoryNotFoundException>(resolver.Resolve);

        Assert.Contains(Path.Combine(baseDirectory, "skills"), exception.Message, StringComparison.Ordinal);
    }
}

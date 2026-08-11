using MackySoft.AgentDistribution.Distribution;

namespace MackySoft.AgentDistribution.Tests.Distribution;

public sealed class BundledAgentDistributionPackageRootResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_ReturnsAgentDistributionDirectoryDirectlyUnderBaseDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "bundled-root");
        var baseDirectory = scope.CreateDirectory("app");
        var agentDistributionDirectory = AbsolutePath.Parse(scope.CreateDirectory("app/agent-distribution"));
        var resolver = new BundledAgentDistributionPackageRootResolver(AbsolutePath.Parse(baseDirectory));

        var result = resolver.Resolve();

        Assert.Equal(agentDistributionDirectory, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_DoesNotWalkUpToParentAgentDistributionDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("agent-distribution-skills", "bundled-root-parent");
        var baseDirectory = scope.CreateDirectory("app");
        scope.CreateDirectory("agent-distribution");
        var resolver = new BundledAgentDistributionPackageRootResolver(AbsolutePath.Parse(baseDirectory));

        var exception = Assert.Throws<DirectoryNotFoundException>(resolver.Resolve);

        Assert.Contains(Path.Combine(baseDirectory, "agent-distribution"), exception.Message, StringComparison.Ordinal);
    }
}

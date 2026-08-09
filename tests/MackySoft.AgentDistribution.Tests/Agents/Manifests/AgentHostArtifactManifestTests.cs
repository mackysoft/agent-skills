using MackySoft.AgentDistribution.Agents.Manifests;
using MackySoft.AgentDistribution.Digests;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Agents.Manifests;

public sealed class AgentHostArtifactManifestTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_DerivesHostTargetRelativePathFromCanonicalPackagePath ()
    {
        var artifact = new AgentHostArtifactManifest(
            HostKind.GitHubCopilot,
            PackageRelativePath.Parse("hosts/github-copilot/profiles/architect.agent.md"),
            Sha256Digest.Parse(new string('a', 64)));

        Assert.Equal("profiles/architect.agent.md", artifact.HostTargetRelativePath.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsPackagePathForDifferentHost ()
    {
        Assert.Throws<ArgumentException>(() => new AgentHostArtifactManifest(
            HostKind.Codex,
            PackageRelativePath.Parse("hosts/claude-code/architect.md"),
            Sha256Digest.Parse(new string('a', 64))));
    }
}

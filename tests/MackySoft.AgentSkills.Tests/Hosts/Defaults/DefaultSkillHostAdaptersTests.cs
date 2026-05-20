using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Defaults;

namespace MackySoft.AgentSkills.Tests.Hosts.Defaults;

public sealed class DefaultSkillHostAdaptersTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Descriptors_ProjectSupportedHostPayloadWithoutAdapterSwitches ()
    {
        var payloads = DefaultSkillHostAdapters.CreateSet()
            .Adapters
            .Select(static adapter => CreatePayload(adapter.Descriptor))
            .OrderBy(static payload => payload.Host, StringComparer.Ordinal)
            .ToArray();

        Assert.Collection(
            payloads,
            static payload =>
            {
                Assert.Equal("claude", payload.Host);
                Assert.True(payload.SupportsProjectScope);
                Assert.True(payload.SupportsUserScope);
                Assert.Equal(".claude/skills", payload.ProjectDefaultTargetPath);
                Assert.Equal("~/.claude/skills", payload.UserDefaultTargetPath);
                Assert.False(payload.RequiresMetadataArtifact);
                Assert.Null(payload.MetadataArtifactPath);
                Assert.False(string.IsNullOrWhiteSpace(payload.ReloadGuidance));
            },
            static payload =>
            {
                Assert.Equal("copilot", payload.Host);
                Assert.True(payload.SupportsProjectScope);
                Assert.True(payload.SupportsUserScope);
                Assert.Equal(".github/skills", payload.ProjectDefaultTargetPath);
                Assert.Equal("~/.copilot/skills", payload.UserDefaultTargetPath);
                Assert.False(payload.RequiresMetadataArtifact);
                Assert.Null(payload.MetadataArtifactPath);
                Assert.False(string.IsNullOrWhiteSpace(payload.ReloadGuidance));
            },
            static payload =>
            {
                Assert.Equal("openai", payload.Host);
                Assert.True(payload.SupportsProjectScope);
                Assert.True(payload.SupportsUserScope);
                Assert.Equal(".agents/skills", payload.ProjectDefaultTargetPath);
                Assert.Equal("${CODEX_HOME}/skills or ~/.codex/skills", payload.UserDefaultTargetPath);
                Assert.True(payload.RequiresMetadataArtifact);
                Assert.Equal("agents/openai.yaml", payload.MetadataArtifactPath);
                Assert.False(string.IsNullOrWhiteSpace(payload.ReloadGuidance));
            });
    }

    private static SupportedHostPayload CreatePayload (SkillHostDescriptor descriptor)
    {
        return new SupportedHostPayload(
            descriptor.HostKey,
            descriptor.SupportsProjectScope,
            descriptor.SupportsUserScope,
            descriptor.ProjectDefaultTargetPath,
            descriptor.UserDefaultTargetPath,
            descriptor.RequiresMetadataArtifact,
            descriptor.MetadataArtifactPath,
            descriptor.ReloadGuidance);
    }

    private sealed record SupportedHostPayload (
        string Host,
        bool SupportsProjectScope,
        bool SupportsUserScope,
        string? ProjectDefaultTargetPath,
        string? UserDefaultTargetPath,
        bool RequiresMetadataArtifact,
        string? MetadataArtifactPath,
        string ReloadGuidance);
}

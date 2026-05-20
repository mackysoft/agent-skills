using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Tests.Hosts.Registration;

public sealed class SkillHostAdapterSetTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AdapterSet_UsesInjectedAdaptersOnlyInDeterministicOrder ()
    {
        var adapterSet = new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter("beta", ".beta/skills"),
            new TestSkillHostAdapter("alpha", ".alpha/skills"),
        ]);

        Assert.Equal(
            new[] { "alpha", "beta" },
            adapterSet.Adapters.Select(static adapter => adapter.Descriptor.HostKey).ToArray());

        Assert.Equal(".alpha/skills", adapterSet.GetAdapter("alpha").Value!.Descriptor.ProjectDefaultTargetPath);
        Assert.Equal(".beta/skills", adapterSet.GetAdapter("beta").Value!.Descriptor.ProjectDefaultTargetPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetAdapter_ReturnsUnsupportedHostFailure ()
    {
        var adapterSet = new SkillHostAdapterSet([new TestSkillHostAdapter("alpha", ".alpha/skills")]);

        var result = adapterSet.GetAdapter("generic");

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetAdapter_CanonicalizesHostKey ()
    {
        var adapterSet = new SkillHostAdapterSet([new TestSkillHostAdapter("openai", ".agents/skills")]);

        var result = adapterSet.GetAdapter("OpenAI");

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal("openai", result.Value!.Descriptor.HostKey);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_AllowsEnvironmentVariableRootWithoutChildDirectory ()
    {
        var adapterSet = new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter(
                "alpha",
                ".alpha/skills",
                new SkillUserTargetRootPolicy("ALPHA_HOME", null, ".alpha/skills")),
        ]);

        Assert.Equal("alpha", adapterSet.Adapters.Single().Descriptor.HostKey);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsUnsafeHomeRelativeUserTargetDirectory ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter(
                "alpha",
                ".alpha/skills",
                new SkillUserTargetRootPolicy(null, null, "../outside")),
        ]));

        Assert.Contains("home-relative user target directory", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsEnvironmentChildDirectoryWithoutEnvironmentVariable ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter(
                "alpha",
                ".alpha/skills",
                new SkillUserTargetRootPolicy(null, "skills", ".alpha/skills")),
        ]));

        Assert.Contains("requires an environment variable name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsDescriptorWithoutSupportedScopes ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter(new SkillHostDescriptor(
                HostKey: "alpha",
                SupportsProjectScope: false,
                SupportsUserScope: false,
                ProjectDefaultTargetPath: null,
                UserDefaultTargetPath: null,
                UserTargetRootPolicy: null,
                RequiresMetadataArtifact: false,
                MetadataArtifactPath: null,
                ReloadGuidance: "Reload alpha skills.")),
        ]));

        Assert.Contains("must support at least one install scope", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsProjectDefaultTargetPathWhenProjectScopeUnsupported ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter(new SkillHostDescriptor(
                HostKey: "alpha",
                SupportsProjectScope: false,
                SupportsUserScope: true,
                ProjectDefaultTargetPath: ".alpha/skills",
                UserDefaultTargetPath: "~/.alpha/skills",
                UserTargetRootPolicy: new SkillUserTargetRootPolicy(null, null, ".alpha/skills"),
                RequiresMetadataArtifact: false,
                MetadataArtifactPath: null,
                ReloadGuidance: "Reload alpha skills.")),
        ]));

        Assert.Contains("project default target path must be null", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsMissingProjectDefaultTargetPathWhenProjectScopeSupported ()
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter(new SkillHostDescriptor(
                HostKey: "alpha",
                SupportsProjectScope: true,
                SupportsUserScope: true,
                ProjectDefaultTargetPath: null,
                UserDefaultTargetPath: "~/.alpha/skills",
                UserTargetRootPolicy: new SkillUserTargetRootPolicy(null, null, ".alpha/skills"),
                RequiresMetadataArtifact: false,
                MetadataArtifactPath: null,
                ReloadGuidance: "Reload alpha skills.")),
        ]));

        Assert.Contains("project default target path is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsUserDefaultTargetMetadataWhenUserScopeUnsupported ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter(new SkillHostDescriptor(
                HostKey: "alpha",
                SupportsProjectScope: true,
                SupportsUserScope: false,
                ProjectDefaultTargetPath: ".alpha/skills",
                UserDefaultTargetPath: "~/.alpha/skills",
                UserTargetRootPolicy: null,
                RequiresMetadataArtifact: false,
                MetadataArtifactPath: null,
                ReloadGuidance: "Reload alpha skills.")),
        ]));

        Assert.Contains("user default target metadata must be null", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsMissingUserTargetRootPolicyWhenUserScopeSupported ()
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter(new SkillHostDescriptor(
                HostKey: "alpha",
                SupportsProjectScope: true,
                SupportsUserScope: true,
                ProjectDefaultTargetPath: ".alpha/skills",
                UserDefaultTargetPath: "~/.alpha/skills",
                UserTargetRootPolicy: null,
                RequiresMetadataArtifact: false,
                MetadataArtifactPath: null,
                ReloadGuidance: "Reload alpha skills.")),
        ]));

        Assert.Contains("user target root policy is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsMetadataArtifactPathWhenMetadataArtifactNotRequired ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter(new SkillHostDescriptor(
                HostKey: "alpha",
                SupportsProjectScope: true,
                SupportsUserScope: true,
                ProjectDefaultTargetPath: ".alpha/skills",
                UserDefaultTargetPath: "~/.alpha/skills",
                UserTargetRootPolicy: new SkillUserTargetRootPolicy(null, null, ".alpha/skills"),
                RequiresMetadataArtifact: false,
                MetadataArtifactPath: "agents/alpha.yaml",
                ReloadGuidance: "Reload alpha skills.")),
        ]));

        Assert.Contains("metadata artifact path must be null", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsMissingMetadataArtifactPathWhenMetadataArtifactRequired ()
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter(new SkillHostDescriptor(
                HostKey: "alpha",
                SupportsProjectScope: true,
                SupportsUserScope: true,
                ProjectDefaultTargetPath: ".alpha/skills",
                UserDefaultTargetPath: "~/.alpha/skills",
                UserTargetRootPolicy: new SkillUserTargetRootPolicy(null, null, ".alpha/skills"),
                RequiresMetadataArtifact: true,
                MetadataArtifactPath: null,
                ReloadGuidance: "Reload alpha skills.")),
        ]));

        Assert.Contains("metadata artifact path is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsUnsafeMetadataArtifactPath ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter(new SkillHostDescriptor(
                HostKey: "alpha",
                SupportsProjectScope: true,
                SupportsUserScope: true,
                ProjectDefaultTargetPath: ".alpha/skills",
                UserDefaultTargetPath: "~/.alpha/skills",
                UserTargetRootPolicy: new SkillUserTargetRootPolicy(null, null, ".alpha/skills"),
                RequiresMetadataArtifact: true,
                MetadataArtifactPath: "../alpha.yaml",
                ReloadGuidance: "Reload alpha skills.")),
        ]));

        Assert.Contains("metadata artifact path must be a safe relative path", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsLegacyMetadataArtifactPathMismatch ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkillHostAdapterSet(
        [
            new LegacyMetadataArtifactPathHostAdapter(
                descriptorMetadataArtifactPath: "agents/alpha.yaml",
                legacyMetadataArtifactPath: "agents/beta.yaml"),
        ]));

        Assert.Contains("metadata artifact path must match descriptor metadata artifact path", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsEmptyAdapters ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkillHostAdapterSet([]));

        Assert.Contains("At least one host adapter", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsDuplicateHostKeys ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter("openai", ".agents/skills"),
            new TestSkillHostAdapter("OpenAI", ".other/skills"),
        ]));

        Assert.Contains("Host adapter key must be unique", exception.Message, StringComparison.Ordinal);
    }

    private sealed class TestSkillHostAdapter : ISkillHostAdapter
    {
        public TestSkillHostAdapter (
            string hostKey,
            string projectTargetDirectory,
            SkillUserTargetRootPolicy? userTargetRootPolicy = null)
            : this(new SkillHostDescriptor(
                HostKey: hostKey,
                SupportsProjectScope: true,
                SupportsUserScope: true,
                ProjectDefaultTargetPath: projectTargetDirectory,
                UserDefaultTargetPath: "~/.test/skills",
                UserTargetRootPolicy: userTargetRootPolicy ?? new SkillUserTargetRootPolicy(null, null, ".test/skills"),
                RequiresMetadataArtifact: false,
                MetadataArtifactPath: null,
                ReloadGuidance: "Reload test skills."))
        {
        }

        public TestSkillHostAdapter (SkillHostDescriptor descriptor)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        }

        public SkillHostDescriptor Descriptor { get; }

        public SkillHostArtifactSet BuildArtifacts (SkillHostMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            return new SkillHostArtifactSet(string.Empty, null);
        }
    }

    private sealed class LegacyMetadataArtifactPathHostAdapter : ISkillHostAdapter
    {
        public LegacyMetadataArtifactPathHostAdapter (
            string descriptorMetadataArtifactPath,
            string legacyMetadataArtifactPath)
        {
            Descriptor = new SkillHostDescriptor(
                HostKey: "alpha",
                SupportsProjectScope: true,
                SupportsUserScope: true,
                ProjectDefaultTargetPath: ".alpha/skills",
                UserDefaultTargetPath: "~/.alpha/skills",
                UserTargetRootPolicy: new SkillUserTargetRootPolicy(null, null, ".alpha/skills"),
                RequiresMetadataArtifact: true,
                MetadataArtifactPath: descriptorMetadataArtifactPath,
                ReloadGuidance: "Reload alpha skills.");
            MetadataArtifactPath = legacyMetadataArtifactPath;
        }

        public SkillHostDescriptor Descriptor { get; }

        public string? MetadataArtifactPath { get; }

        public SkillHostArtifactSet BuildArtifacts (SkillHostMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            return new SkillHostArtifactSet(string.Empty, string.Empty);
        }
    }
}

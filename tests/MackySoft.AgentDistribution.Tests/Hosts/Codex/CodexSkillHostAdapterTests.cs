using MackySoft.AgentDistribution.Hosts.Codex;
using MackySoft.AgentDistribution.Hosts.Registration;

namespace MackySoft.AgentDistribution.Tests.Hosts.Codex;

public sealed class CodexSkillHostAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Registration_ExposesCodexSkillPolicy ()
    {
        var registration = HostRegistration.Get(HostKind.Codex).Value!;
        var descriptor = registration.Skill;

        Assert.IsType<CodexSkillHostAdapter>(registration.SkillAdapter);
        Assert.Equal(HostKind.Codex, registration.Host);
        Assert.Equal(".agents/skills", descriptor.ProjectDefaultTargetPath.Value);
        Assert.Equal("CODEX_HOME", descriptor.UserTargetRootPolicy.EnvironmentVariableName);
        Assert.Equal("skills", descriptor.UserTargetRootPolicy.EnvironmentVariableChildDirectory!.Value);
        Assert.Equal(".codex/skills", descriptor.UserTargetRootPolicy.HomeRelativeDirectory.Value);
        Assert.Equal("agents/openai.yaml", descriptor.MetadataArtifactPath!.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_UsesDeterministicYaml ()
    {
        var metadata = new SkillHostMetadata(
            new SkillName("agent-distribution-sample"),
            "Sample \"Skill\"\rName",
            "Use C:\\Unity\r\nNext");

        var artifacts = new CodexSkillHostAdapter().BuildArtifacts(metadata);

        Assert.Equal(
            "---\n"
            + "name: \"agent-distribution-sample\"\n"
            + "description: \"Use C:\\\\Unity\\r\\nNext\"\n"
            + "---\n",
            artifacts.Frontmatter);
        Assert.Contains("default_prompt: \"Use $agent-distribution-sample", artifacts.MetadataContent, StringComparison.Ordinal);
    }
}

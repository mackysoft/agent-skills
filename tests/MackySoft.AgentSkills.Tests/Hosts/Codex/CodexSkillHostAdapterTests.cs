using MackySoft.AgentSkills.Hosts.Codex;
using MackySoft.AgentSkills.Hosts.Registration;

namespace MackySoft.AgentSkills.Tests.Hosts.Codex;

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
            new SkillName("agent-skills-sample"),
            "Sample \"Skill\"\rName",
            "Use C:\\Unity\r\nNext");

        var artifacts = new CodexSkillHostAdapter().BuildArtifacts(metadata);

        Assert.Equal(
            "---\n"
            + "name: \"agent-skills-sample\"\n"
            + "description: \"Use C:\\\\Unity\\r\\nNext\"\n"
            + "---\n",
            artifacts.Frontmatter);
        Assert.Contains("default_prompt: \"Use $agent-skills-sample", artifacts.MetadataContent, StringComparison.Ordinal);
    }
}

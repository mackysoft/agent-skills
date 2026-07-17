using MackySoft.AgentSkills.Hosting.Commands;

namespace MackySoft.AgentSkills.Tests.Hosting;

public sealed class AgentSkillsCommandRequestTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Requests_CaptureOptionValueSnapshots ()
    {
        var categories = new List<string> { "core" };
        var skills = new List<string> { "sample-skill" };
        var list = new AgentSkillsListCommandRequest(category: categories, skill: skills);
        var export = new AgentSkillsExportCommandRequest(category: categories, skill: skills);
        var install = new AgentSkillsInstallCommandRequest(category: categories, skill: skills);
        var update = new AgentSkillsUpdateCommandRequest(category: categories, skill: skills);
        var uninstall = new AgentSkillsUninstallCommandRequest(category: categories, skill: skills);
        var prune = new AgentSkillsPruneCommandRequest(category: categories, skill: skills);
        var doctor = new AgentSkillsDoctorCommandRequest(category: categories, skill: skills);

        categories.Clear();
        skills.Clear();

        IReadOnlyList<IReadOnlyList<string>?> categorySnapshots =
        [
            list.Category,
            export.Category,
            install.Category,
            update.Category,
            uninstall.Category,
            prune.Category,
            doctor.Category,
        ];
        IReadOnlyList<IReadOnlyList<string>?> skillSnapshots =
        [
            list.Skill,
            export.Skill,
            install.Skill,
            update.Skill,
            uninstall.Skill,
            prune.Skill,
            doctor.Skill,
        ];

        Assert.All(categorySnapshots, static snapshot => Assert.Equal(["core"], snapshot));
        Assert.All(skillSnapshots, static snapshot => Assert.Equal(["sample-skill"], snapshot));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Request_RejectsNullOptionItems ()
    {
        Assert.Throws<ArgumentException>(() => new AgentSkillsListCommandRequest(category: new string[] { null! }));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequestsAndOutputOptions_ExposeOnlyGetOnlyProperties ()
    {
        Type[] types =
        [
            typeof(AgentSkillsListCommandRequest),
            typeof(AgentSkillsExportCommandRequest),
            typeof(AgentSkillsInstallCommandRequest),
            typeof(AgentSkillsUpdateCommandRequest),
            typeof(AgentSkillsUninstallCommandRequest),
            typeof(AgentSkillsPruneCommandRequest),
            typeof(AgentSkillsDoctorCommandRequest),
            typeof(AgentSkillsCommandOutputOptions),
        ];

        Assert.All(types, static type =>
            Assert.All(type.GetProperties(), static property => Assert.Null(property.SetMethod)));
    }
}

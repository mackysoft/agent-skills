using MackySoft.AgentSkills.Hosting.Commands;

namespace MackySoft.AgentSkills.Tests.Hosting;

public sealed class SkillCommandRequestTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Request_CapturesOptionValueSnapshots ()
    {
        var categories = new List<string> { "core" };
        var skills = new List<string> { "sample-skill" };
        var request = new SkillInstallCommandRequest(category: categories, skill: skills);

        categories.Clear();
        skills.Clear();

        Assert.Equal(["core"], request.Category);
        Assert.Equal(["sample-skill"], request.Skill);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Request_RejectsNullOptionItems ()
    {
        Assert.Throws<ArgumentException>(() => new SkillListCommandRequest(category: new string[] { null! }));
    }
}

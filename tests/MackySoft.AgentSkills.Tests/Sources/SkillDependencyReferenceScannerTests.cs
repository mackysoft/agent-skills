using MackySoft.AgentSkills.Sources;

namespace MackySoft.AgentSkills.Tests.Sources;

public sealed class SkillDependencyReferenceScannerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void FindReferences_DetectsReferencesBeforePunctuation ()
    {
        var references = SkillDependencyReferenceScanner.FindReferences(
        [
            "Use $target-skill, then ($other-skill). Finish with $third-skill.",
        ]);

        Assert.Equal(
            ["other-skill", "target-skill", "third-skill"],
            references.Select(static skillName => skillName.Value).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FindReferences_DoesNotReturnPartialMatches ()
    {
        var references = SkillDependencyReferenceScanner.FindReferences(
        [
            "Ignore $target-skill_extra, $target-skillA, and $target-skill_ suffixes.",
        ]);

        Assert.Empty(references);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FindReferences_DeduplicatesAndSortsReferences ()
    {
        var references = SkillDependencyReferenceScanner.FindReferences(
        [
            "Use $z-skill and $a-skill.",
            "Use $z-skill again.",
        ]);

        Assert.Equal(["a-skill", "z-skill"], references.Select(static skillName => skillName.Value).ToArray());
    }
}

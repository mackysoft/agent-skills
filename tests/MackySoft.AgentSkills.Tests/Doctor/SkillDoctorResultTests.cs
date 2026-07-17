using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosts.Contracts;

namespace MackySoft.AgentSkills.Tests.Doctor;

public sealed class SkillDoctorResultTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_CapturesDiagnosticSnapshotAndComputesHealth ()
    {
        var diagnostics = new List<SkillDoctorDiagnostic>
        {
            SkillDoctorDiagnostic.Info("SKILL_OK", "Healthy."),
        };
        var targetRoot = Path.Combine(Path.GetTempPath(), "doctor", "..", "target");
        var result = new SkillDoctorResult(SkillHostKind.OpenAi, targetRoot, diagnostics);

        diagnostics[0] = SkillDoctorDiagnostic.Error("SKILL_ERROR", "Broken.");

        Assert.True(result.IsHealthy);
        Assert.Equal("SKILL_OK", Assert.Single(result.Diagnostics).Code.Value);
        Assert.Equal(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "target")), result.TargetRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsInvalidHostTargetAndDiagnostics ()
    {
        var targetRoot = Path.GetFullPath("target");

        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillDoctorResult((SkillHostKind)42, targetRoot, []));
        Assert.Throws<ArgumentException>(() => new SkillDoctorResult(SkillHostKind.OpenAi, " ", []));
        Assert.Throws<ArgumentException>(() => new SkillDoctorResult(SkillHostKind.OpenAi, "relative", []));
        Assert.Throws<ArgumentException>(() => new SkillDoctorResult(SkillHostKind.OpenAi, targetRoot, [null!]));
    }
}

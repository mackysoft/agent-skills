using MackySoft.AgentDistribution.Doctor;

namespace MackySoft.AgentDistribution.Tests.Doctor;

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
        var result = new SkillDoctorResult(HostKind.Codex, AbsolutePath.Parse(targetRoot), diagnostics);

        diagnostics[0] = SkillDoctorDiagnostic.Error("SKILL_ERROR", "Broken.");

        Assert.True(result.IsHealthy);
        Assert.Equal("SKILL_OK", Assert.Single(result.Diagnostics).Code.Value);
        Assert.Equal(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "target")), result.TargetRoot.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsInvalidHostTargetAndDiagnostics ()
    {
        var targetRoot = Path.GetFullPath("target");

        Assert.Throws<ArgumentOutOfRangeException>(() => new SkillDoctorResult((HostKind)42, AbsolutePath.Parse(targetRoot), []));
        Assert.Throws<ArgumentNullException>(() => new SkillDoctorResult(HostKind.Codex, null!, []));
        Assert.Throws<PathValidationException>(() => AbsolutePath.Parse("relative"));
        Assert.Throws<ArgumentException>(() => new SkillDoctorResult(HostKind.Codex, AbsolutePath.Parse(targetRoot), [null!]));
    }
}

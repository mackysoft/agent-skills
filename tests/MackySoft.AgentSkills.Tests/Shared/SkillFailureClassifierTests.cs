using MackySoft.AgentSkills.Shared;
using MackySoft.Tests;

namespace MackySoft.AgentSkills.Tests.Shared;

public sealed class SkillFailureClassifierTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Classify_CoversEveryKnownFailureCode ()
    {
        var expectedCategories = CreateExpectedCategories();
        var knownCodes = StaticFieldValueReader.ReadFromStaticClasses<SkillFailureCode>(
            typeof(SkillFailureCodes).Assembly,
            "Codes");

        Assert.Equal(
            knownCodes.OrderBy(static code => code.Value).ToArray(),
            expectedCategories.Keys.OrderBy(static code => code.Value).ToArray());

        foreach (var (code, expectedCategory) in expectedCategories)
        {
            Assert.Equal(expectedCategory, SkillFailureClassifier.Classify(code));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Classify_Failure_UsesFailureCode ()
    {
        var failure = SkillFailure.Create(SkillFailureCodes.PathUnsafe, "Unsafe path.");

        var category = SkillFailureClassifier.Classify(failure);

        Assert.Equal(SkillFailureCategory.UnsafePath, category);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Classify_ReturnsUnexpectedInternalFailure_ForUnknownCode ()
    {
        var category = SkillFailureClassifier.Classify(new SkillFailureCode("SKILL_FUTURE_FAILURE"));

        Assert.Equal(SkillFailureCategory.UnexpectedInternalFailure, category);
    }

    private static IReadOnlyDictionary<SkillFailureCode, SkillFailureCategory> CreateExpectedCategories ()
    {
        return new Dictionary<SkillFailureCode, SkillFailureCategory>
        {
            [SkillFailureCodes.InputInvalid] = SkillFailureCategory.InvalidInput,
            [SkillFailureCodes.PathUnsafe] = SkillFailureCategory.UnsafePath,
            [SkillFailureCodes.HostUnsupported] = SkillFailureCategory.UnsupportedHost,
            [SkillFailureCodes.ScopeUnsupported] = SkillFailureCategory.UnsupportedScope,
            [SkillFailureCodes.UserTargetUnavailable] = SkillFailureCategory.UserTargetUnavailable,
            [SkillFailureCodes.ManifestInvalid] = SkillFailureCategory.ManifestInvalid,
            [SkillFailureCodes.SourceInvalid] = SkillFailureCategory.SourceInvalid,
            [SkillFailureCodes.InstallTargetDigestMismatch] = SkillFailureCategory.DriftOrLocalModification,
            [SkillFailureCodes.InstallTargetManifestDigestMismatch] = SkillFailureCategory.DriftOrLocalModification,
            [SkillFailureCodes.InstallTargetContentDigestMismatch] = SkillFailureCategory.DriftOrLocalModification,
            [SkillFailureCodes.InstallTargetFrontmatterDigestMismatch] = SkillFailureCategory.DriftOrLocalModification,
            [SkillFailureCodes.InstallTargetHostArtifactDigestMismatch] = SkillFailureCategory.DriftOrLocalModification,
            [SkillFailureCodes.InstallTargetFileSetMismatch] = SkillFailureCategory.DriftOrLocalModification,
            [SkillFailureCodes.InstallTargetOutdated] = SkillFailureCategory.DriftOrLocalModification,
            [SkillFailureCodes.InstallTargetVersionAhead] = SkillFailureCategory.DriftOrLocalModification,
            [SkillFailureCodes.InstallTargetLocalModification] = SkillFailureCategory.DriftOrLocalModification,
            [SkillFailureCodes.InstallTargetRemovedFromCatalog] = SkillFailureCategory.RemovedFromCatalog,
            [SkillFailureCodes.InstallTargetUnmanaged] = SkillFailureCategory.UnmanagedTarget,
            [SkillFailureCodes.InstallTargetNameCollision] = SkillFailureCategory.NameCollision,
            [SkillFailureCodes.InstallTargetHostConflict] = SkillFailureCategory.HostConflict,
            [SkillFailureCodes.InstallTargetReadFailed] = SkillFailureCategory.ReadFailure,
            [SkillFailureCodes.InstallTargetWriteFailed] = SkillFailureCategory.WriteOrFileSystemFailure,
        };
    }
}

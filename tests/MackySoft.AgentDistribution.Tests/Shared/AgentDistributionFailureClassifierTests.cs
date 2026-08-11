using System.Reflection;
using MackySoft.AgentDistribution.Shared;

namespace MackySoft.AgentDistribution.Tests.Shared;

public sealed class AgentDistributionFailureClassifierTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Classify_CoversEveryKnownFailureCode ()
    {
        var expectedCategories = CreateExpectedCategories();
        var knownCodes = typeof(AgentDistributionFailureCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.FieldType == typeof(AgentDistributionFailureCode))
            .Select(static field => (AgentDistributionFailureCode)field.GetValue(null)!)
            .ToHashSet();

        Assert.Equal(
            knownCodes.OrderBy(static code => code.Value).ToArray(),
            expectedCategories.Keys.OrderBy(static code => code.Value).ToArray());

        foreach (var (code, expectedCategory) in expectedCategories)
        {
            Assert.Equal(expectedCategory, AgentDistributionFailureClassifier.Classify(code));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Classify_Failure_UsesFailureCode ()
    {
        var failure = AgentDistributionFailure.Create(AgentDistributionFailureCodes.PathUnsafe, "Unsafe path.");

        var category = AgentDistributionFailureClassifier.Classify(failure);

        Assert.Equal(AgentDistributionFailureCategory.UnsafePath, category);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Classify_ReturnsUnexpectedInternalFailure_ForUnknownCode ()
    {
        var category = AgentDistributionFailureClassifier.Classify(new AgentDistributionFailureCode("AGENT_DISTRIBUTION_FUTURE_FAILURE"));

        Assert.Equal(AgentDistributionFailureCategory.UnexpectedInternalFailure, category);
    }

    private static IReadOnlyDictionary<AgentDistributionFailureCode, AgentDistributionFailureCategory> CreateExpectedCategories ()
    {
        return new Dictionary<AgentDistributionFailureCode, AgentDistributionFailureCategory>
        {
            [AgentDistributionFailureCodes.InputInvalid] = AgentDistributionFailureCategory.InvalidInput,
            [AgentDistributionFailureCodes.PathUnsafe] = AgentDistributionFailureCategory.UnsafePath,
            [AgentDistributionFailureCodes.HostUnsupported] = AgentDistributionFailureCategory.UnsupportedHost,
            [AgentDistributionFailureCodes.ScopeUnsupported] = AgentDistributionFailureCategory.UnsupportedScope,
            [AgentDistributionFailureCodes.UserTargetUnavailable] = AgentDistributionFailureCategory.UserTargetUnavailable,
            [AgentDistributionFailureCodes.ManifestInvalid] = AgentDistributionFailureCategory.ManifestInvalid,
            [AgentDistributionFailureCodes.SourceInvalid] = AgentDistributionFailureCategory.SourceInvalid,
            [AgentDistributionFailureCodes.BundleVersionConflict] = AgentDistributionFailureCategory.SourceInvalid,
            [AgentDistributionFailureCodes.BundleUpdateRequired] = AgentDistributionFailureCategory.DriftOrLocalModification,
            [AgentDistributionFailureCodes.InstallTargetDigestMismatch] = AgentDistributionFailureCategory.DriftOrLocalModification,
            [AgentDistributionFailureCodes.InstallTargetManifestDigestMismatch] = AgentDistributionFailureCategory.DriftOrLocalModification,
            [AgentDistributionFailureCodes.InstallTargetContentDigestMismatch] = AgentDistributionFailureCategory.DriftOrLocalModification,
            [AgentDistributionFailureCodes.InstallTargetFrontmatterDigestMismatch] = AgentDistributionFailureCategory.DriftOrLocalModification,
            [AgentDistributionFailureCodes.InstallTargetHostArtifactDigestMismatch] = AgentDistributionFailureCategory.DriftOrLocalModification,
            [AgentDistributionFailureCodes.InstallTargetFileSetMismatch] = AgentDistributionFailureCategory.DriftOrLocalModification,
            [AgentDistributionFailureCodes.InstallTargetOutdated] = AgentDistributionFailureCategory.DriftOrLocalModification,
            [AgentDistributionFailureCodes.InstallTargetVersionAhead] = AgentDistributionFailureCategory.DriftOrLocalModification,
            [AgentDistributionFailureCodes.InstallTargetLocalModification] = AgentDistributionFailureCategory.DriftOrLocalModification,
            [AgentDistributionFailureCodes.InstallTargetRemovedFromCatalog] = AgentDistributionFailureCategory.RemovedFromCatalog,
            [AgentDistributionFailureCodes.InstallTargetUnmanaged] = AgentDistributionFailureCategory.UnmanagedTarget,
            [AgentDistributionFailureCodes.InstallTargetNameCollision] = AgentDistributionFailureCategory.NameCollision,
            [AgentDistributionFailureCodes.InstallTargetHostConflict] = AgentDistributionFailureCategory.HostConflict,
            [AgentDistributionFailureCodes.InstallTargetRootConflict] = AgentDistributionFailureCategory.TargetRootConflict,
            [AgentDistributionFailureCodes.InstallTargetReadFailed] = AgentDistributionFailureCategory.ReadFailure,
            [AgentDistributionFailureCodes.InstallTargetWriteFailed] = AgentDistributionFailureCategory.WriteOrFileSystemFailure,
        };
    }
}

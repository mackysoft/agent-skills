using System.Reflection;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.OperationReports.Literals;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Tests.Shared;

public sealed class ContractLiteralCodecTests
{
    private static readonly Type[] ContractLiteralEnumTypes = typeof(ContractLiteralAttribute).Assembly
        .GetTypes()
        .Where(static type => type.IsEnum && type
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Any(static field => field.IsDefined(typeof(ContractLiteralAttribute), inherit: false)))
        .OrderBy(static type => type.FullName, StringComparer.Ordinal)
        .ToArray();

    public static TheoryData<Type, string[]> ContractLiteralEnumContracts => new()
    {
        { typeof(SkillHostKind), new[] { "claude", "copilot", "openai" } },
        { typeof(SkillScopeKind), new[] { "project", "user" } },
        { typeof(SkillExportFormat), new[] { "directory", "zip" } },
        { typeof(SkillInstallActionKind), new[] { "created", "updated", "noOp", "blockedManagedOverwrite", "blockedLocalModification", "blockedUnmanaged" } },
        { typeof(SkillUpdateActionKind), new[] { "created", "updated", "noOp", "blockedLocalModification", "blockedUnmanaged", "blockedVersionAhead" } },
        { typeof(SkillUninstallActionKind), new[] { "deleted", "noOp", "skippedUnmanaged", "blockedLocalModification" } },
        {
            typeof(SkillPruneActionKind),
            new[]
            {
                "deleted",
                "skippedCurrent",
                "skippedForeignCatalog",
                "skippedUnmanaged",
                "blockedLocalModification",
                "blockedManifestInvalid",
                "blockedNameCollision",
                "blockedHostConflict",
            }
        },
        { typeof(SkillOperationActionStatus), new[] { "changed", "noOp", "skipped", "blocked" } },
        {
            typeof(SkillBlockedReason),
            new[]
            {
                "managedOverwriteRequiresForce",
                "localModificationRequiresForce",
                "unmanagedTarget",
                "installedVersionAhead",
            }
        },
        {
            typeof(SkillTargetStateKind),
            new[]
            {
                "missing",
                "current",
                "cleanOutdated",
                "localModification",
                "unmanagedTarget",
                "manifestDrift",
                "commonContentDrift",
                "frontmatterDrift",
                "hostArtifactDrift",
                "fileSetDrift",
                "nameCollision",
                "hostConflict",
                "versionAhead",
                "removedFromCatalog",
            }
        },
        { typeof(SkillDiffChangeKind), new[] { "added", "modified", "deleted" } },
        { typeof(SkillDoctorSeverity), new[] { "info", "error" } },
    };

    [Fact]
    [Trait("Size", "Small")]
    public void ToValue_ReturnsCanonicalLiteral ()
    {
        Assert.Equal("second-value", ContractLiteralCodec.ToValue(SampleLiteral.Second));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryToValue_ReportsWhetherValueIsMapped ()
    {
        Assert.True(ContractLiteralCodec.TryToValue(SampleLiteral.Second, out var literal));
        Assert.Equal("second-value", literal);

        Assert.False(ContractLiteralCodec.TryToValue((SampleLiteral)999, out literal));
        Assert.Null(literal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_ParsesCanonicalLiteralExactly ()
    {
        Assert.True(ContractLiteralCodec.TryParse("firstValue", out SampleLiteral value));
        Assert.Equal(SampleLiteral.First, value);
        Assert.False(ContractLiteralCodec.TryParse("FIRSTVALUE", out value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Matches_RequiresCanonicalLiteralAndExpectedValue ()
    {
        Assert.True(ContractLiteralCodec.Matches("firstValue", SampleLiteral.First));
        Assert.False(ContractLiteralCodec.Matches("FIRSTVALUE", SampleLiteral.First));
        Assert.False(ContractLiteralCodec.Matches("firstValue", SampleLiteral.Second));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsDefined_RequiresCanonicalLiteralOrMappedValue ()
    {
        Assert.True(ContractLiteralCodec.IsDefined<SampleLiteral>("firstValue"));
        Assert.False(ContractLiteralCodec.IsDefined<SampleLiteral>("FIRSTVALUE"));
        Assert.False(ContractLiteralCodec.IsDefined<SampleLiteral>(null));
        Assert.True(ContractLiteralCodec.IsDefined(SampleLiteral.First));
        Assert.False(ContractLiteralCodec.IsDefined((SampleLiteral)999));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetLiterals_ReturnsLiteralsInDeclarationOrder ()
    {
        Assert.Equal(["firstValue", "second-value"], ContractLiteralCodec.GetLiterals<SampleLiteral>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToValue_RejectsUndefinedEnumValue ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ContractLiteralCodec.ToValue((SampleLiteral)999));
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(ContractLiteralEnumContracts))]
    public void GetLiterals_WhenAgentSkillsContractLiteralEnumIsRegistered_ReturnsStableLiterals (
        Type enumType,
        string[] expectedLiterals)
    {
        Assert.Equal(expectedLiterals, InvokeGetLiterals(enumType));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetLiterals_ForEveryDeclaredContractLiteralEnum_BuildsTable ()
    {
        foreach (var enumType in ContractLiteralEnumTypes)
        {
            Assert.NotEmpty(InvokeGetLiterals(enumType));
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(ContractLiteralEnumContracts))]
    public void ToValue_ReturnsAgentSkillsStableLiteral (
        Type enumType,
        string[] expectedLiterals)
    {
        var values = Enum.GetValues(enumType).Cast<Enum>().ToArray();

        Assert.Equal(expectedLiterals.Length, values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            Assert.Equal(expectedLiterals[i], InvokeToValue(values[i]));
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidContractLiteralAccessors))]
    public void GetLiterals_RejectsInvalidEnumContracts (Action access)
    {
        Assert.Throws<InvalidOperationException>(access);
    }

    public static TheoryData<Action> InvalidContractLiteralAccessors =>
    [
        static () => ContractLiteralCodec.GetLiterals<MissingLiteral>(),
        static () => ContractLiteralCodec.GetLiterals<DuplicateLiteral>(),
        static () => ContractLiteralCodec.GetLiterals<DuplicateValue>(),
        static () => ContractLiteralCodec.GetLiterals<EmptyLiteralContract>(),
    ];

    private enum SampleLiteral
    {
        [ContractLiteral("firstValue")]
        First = 0,

        [ContractLiteral("second-value")]
        Second = 1,
    }

    private enum MissingLiteral
    {
        Value = 0,
    }

    private enum DuplicateLiteral
    {
        [ContractLiteral("same")]
        First = 0,

        [ContractLiteral("same")]
        Second = 1,
    }

    private enum DuplicateValue
    {
        [ContractLiteral("first")]
        First = 0,

        [ContractLiteral("second")]
        Second = 0,
    }

    private enum EmptyLiteralContract
    {
    }

    private static string InvokeToValue (Enum value)
    {
        var method = typeof(ContractLiteralCodec)
            .GetMethods()
            .Single(static method => method.Name == nameof(ContractLiteralCodec.ToValue) && method.GetParameters().Length == 1)
            .MakeGenericMethod(value.GetType());

        return (string)method.Invoke(null, [value])!;
    }

    private static IReadOnlyList<string> InvokeGetLiterals (Type enumType)
    {
        var method = typeof(ContractLiteralCodec)
            .GetMethod(nameof(ContractLiteralCodec.GetLiterals), Type.EmptyTypes)!
            .MakeGenericMethod(enumType);

        return (IReadOnlyList<string>)method.Invoke(null, null)!;
    }
}

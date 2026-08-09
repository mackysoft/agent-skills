using MackySoft.AgentDistribution.Installation.Targeting;
using MackySoft.AgentDistribution.Shared;
using MackySoft.FileSystem;

namespace MackySoft.AgentDistribution.Installation.Results;

internal static class SkillActionContractGuard
{
    public static IReadOnlyList<T> Snapshot<T> (IReadOnlyList<T> values, string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        if (values.Any(static value => value is null))
        {
            throw new ArgumentException("The collection must not contain null items.", parameterName);
        }

        return Array.AsReadOnly(values.ToArray());
    }

    public static IReadOnlyList<T>? OptionalSnapshot<T> (IReadOnlyList<T>? values, string parameterName)
        where T : class
    {
        return values is null ? null : Snapshot(values, parameterName);
    }

    public static IReadOnlyList<PackageRelativePath> PathSnapshot (
        IReadOnlyList<PackageRelativePath> paths,
        string parameterName,
        bool sortOrdinal = false)
    {
        var snapshot = Snapshot(paths, parameterName).ToArray();
        if (snapshot.Distinct(PackageRelativePath.PortableFileSystemComparer).Count() != snapshot.Length)
        {
            throw new ArgumentException("The path collection must not contain duplicate items.", parameterName);
        }

        if (sortOrdinal)
        {
            Array.Sort(snapshot, static (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value));
        }

        return Array.AsReadOnly(snapshot);
    }

    public static TEnum ValidateEnum<TEnum> (TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Vocabulary.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Unsupported {typeof(TEnum).Name} value.");
        }

        return value;
    }

    public static int? ValidateVersion (int? version, string parameterName)
    {
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, version, "A SKILL bundle version must be positive.");
        }

        return version;
    }

    public static void ValidateTargetRootMatchesIdentity (
        AbsolutePath targetRoot,
        SkillInstallIdentity identity,
        string parameterName)
    {
        if (!targetRoot.IsSameAs(identity.TargetRoot))
        {
            throw new ArgumentException("Every action identity target root must match the result target root.", parameterName);
        }
    }

    public static bool IsReplacementTargetState (SkillTargetStateKind kind)
    {
        return kind is SkillTargetStateKind.CleanOutdated or SkillTargetStateKind.VersionAhead
            || SkillTargetStateClassifier.IsLocalModificationDrift(kind);
    }

    public static bool IsReplacementTargetStateOrCurrent (SkillTargetStateKind kind)
    {
        return kind == SkillTargetStateKind.Current || IsReplacementTargetState(kind);
    }

    public static void RequireTargetState (
        SkillActionTargetState targetState,
        Func<SkillTargetStateKind, bool> predicate,
        string parameterName,
        string message)
    {
        if (!predicate(targetState.Kind))
        {
            throw new ArgumentException(message, parameterName);
        }
    }

    public static void RequireNull (object? value, string parameterName, string message)
    {
        if (value is not null)
        {
            throw new ArgumentException(message, parameterName);
        }
    }

    public static T RequireNotNull<T> (T? value, string parameterName)
        where T : class
    {
        return value ?? throw new ArgumentNullException(parameterName);
    }
}

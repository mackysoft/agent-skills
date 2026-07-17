using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.AgentSkills.Installation.Results;

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

    public static IReadOnlyList<string> PathSnapshot (
        IReadOnlyList<string> paths,
        string parameterName,
        bool sortOrdinal = false)
    {
        ArgumentNullException.ThrowIfNull(paths, parameterName);
        var snapshot = paths.ToArray();
        foreach (var path in snapshot)
        {
            ValidateRelativePath(path, parameterName);
        }

        if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("The path collection must not contain duplicate items.", parameterName);
        }

        if (sortOrdinal)
        {
            Array.Sort(snapshot, StringComparer.Ordinal);
        }

        return Array.AsReadOnly(snapshot);
    }

    public static void ValidateRelativePath (string path, string parameterName)
    {
        if (!SkillRelativePath.IsSafeFilePath(path)
            || path.Contains(':', StringComparison.Ordinal)
            || path.Any(char.IsControl))
        {
            throw new ArgumentException("The path must be a safe slash-separated path relative to the SKILL directory.", parameterName);
        }
    }

    public static string ValidateTargetRoot (string targetRoot, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot, parameterName);
        if (!Path.IsPathFullyQualified(targetRoot))
        {
            throw new ArgumentException("The target root must be an absolute path.", parameterName);
        }

        return targetRoot;
    }

    public static TEnum ValidateEnum<TEnum> (TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!ContractLiteralCodec.IsDefined(value))
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

    public static SkillInstallIdentity ValidateIdentity (SkillInstallIdentity identity, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(identity, parameterName);
        return identity;
    }

    public static void ValidateTargetRootMatchesIdentity (
        string targetRoot,
        SkillInstallIdentity identity,
        string parameterName)
    {
        if (!string.Equals(targetRoot, identity.TargetRoot, StringComparison.Ordinal))
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

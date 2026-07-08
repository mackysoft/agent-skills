namespace MackySoft.AgentSkills.Hosting.Commands;

internal static class AgentSkillsCommandRootValidator
{
    public static void ThrowIfInvalid (
        string commandRoot,
        string? parameterName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandRoot, parameterName);

        if (!IsSafeCommandRoot(commandRoot))
        {
            throw new ArgumentException($"SKILL command root is invalid: {commandRoot}", parameterName);
        }
    }

    public static string CreateReportRoot (string commandRoot)
    {
        ThrowIfInvalid(commandRoot, nameof(commandRoot));

        return string.Join(".", commandRoot.Split(' ', StringSplitOptions.None));
    }

    private static bool IsSafeCommandRoot (string commandRoot)
    {
        var tokens = commandRoot.Split(' ', StringSplitOptions.None);
        if (tokens.Length == 0)
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (!IsSafeCommandToken(token))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSafeCommandToken (string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (!IsLowerAsciiLetterOrDigit(token[0]))
        {
            return false;
        }

        var previousWasHyphen = false;
        for (var i = 1; i < token.Length; i++)
        {
            var character = token[i];
            if (character == '-')
            {
                if (previousWasHyphen)
                {
                    return false;
                }

                previousWasHyphen = true;
                continue;
            }

            if (!IsLowerAsciiLetterOrDigit(character))
            {
                return false;
            }

            previousWasHyphen = false;
        }

        return !previousWasHyphen;
    }

    private static bool IsLowerAsciiLetterOrDigit (char character)
    {
        return character is (>= 'a' and <= 'z') or (>= '0' and <= '9');
    }
}

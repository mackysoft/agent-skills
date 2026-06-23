namespace MackySoft.AgentSkills.Shared;

internal static class SkillIdentifierValidator
{
    public static bool IsSafeLowercaseHyphenLiteral (string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !IsAsciiLowercaseLetterOrDigit(value[0]))
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            var character = value[i];
            if (character != '-' && !IsAsciiLowercaseLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLowercaseLetterOrDigit (char character)
    {
        return character is (>= 'a' and <= 'z') or (>= '0' and <= '9');
    }
}

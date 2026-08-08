namespace MackySoft.AgentSkills.Shared.Text;

/// <summary> Parses vocabulary text at command input boundaries that accept spelling variations. </summary>
internal static class VocabularyInputParser
{
    public static bool TryParseIgnoreCase<TEnum> (
        string? literal,
        out TEnum value)
        where TEnum : struct, Enum
    {
        if (literal is null)
        {
            value = default;
            return false;
        }

        foreach (var entry in Vocabulary.GetEntries<TEnum>())
        {
            if (string.Equals(literal, entry.Text, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

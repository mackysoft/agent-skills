namespace MackySoft.AgentSkills.Shared.Text;

/// <summary> Parses enum-backed contract literals at input boundaries that accept user spelling variations. </summary>
internal static class ContractLiteralInputParser
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

        foreach (var candidateLiteral in ContractLiteralCodec.GetLiterals<TEnum>())
        {
            if (string.Equals(literal, candidateLiteral, StringComparison.OrdinalIgnoreCase))
            {
                return ContractLiteralCodec.TryParse(candidateLiteral, out value);
            }
        }

        value = default;
        return false;
    }
}

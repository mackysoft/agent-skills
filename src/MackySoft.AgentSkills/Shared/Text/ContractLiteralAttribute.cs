namespace MackySoft.AgentSkills.Shared.Text;

/// <summary> Defines the canonical contract literal for one enum member. </summary>
/// <remarks>
/// Define at least one enum member and apply this attribute to every declared member of an enum consumed by
/// <see cref="ContractLiteralCodec" /> or <see cref="ContractLiteralJsonConverterFactory" />.
/// </remarks>
[AttributeUsage(AttributeTargets.Field)]
public sealed class ContractLiteralAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="ContractLiteralAttribute" /> class. </summary>
    /// <param name="literal"> The canonical contract literal. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="literal" /> is empty, whitespace, or has outer whitespace. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="literal" /> is <see langword="null" />. </exception>
    public ContractLiteralAttribute (string literal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(literal);
        if (char.IsWhiteSpace(literal[0]) || char.IsWhiteSpace(literal[^1]))
        {
            throw new ArgumentException("Contract literal must not have leading or trailing whitespace.", nameof(literal));
        }

        Literal = literal;
    }

    /// <summary> Gets the canonical contract literal. </summary>
    public string Literal { get; }
}

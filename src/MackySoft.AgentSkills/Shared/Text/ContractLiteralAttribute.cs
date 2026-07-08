namespace MackySoft.AgentSkills.Shared.Text;

/// <summary> Defines the canonical contract literal for one enum member. </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class ContractLiteralAttribute : Attribute
{
    /// <summary> Initializes a new instance of the <see cref="ContractLiteralAttribute" /> class. </summary>
    /// <param name="literal"> The canonical contract literal. </param>
    public ContractLiteralAttribute (string literal)
    {
        Literal = literal;
    }

    /// <summary> Gets the canonical contract literal. </summary>
    public string Literal { get; }
}

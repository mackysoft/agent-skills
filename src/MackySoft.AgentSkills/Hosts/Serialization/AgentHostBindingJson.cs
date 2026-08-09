using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.AgentSkills.Shared;

namespace MackySoft.AgentSkills.Hosts.Serialization;

/// <summary>Deserializes strict host-binding JSON while keeping host schemas adapter-owned.</summary>
internal static class AgentHostBindingJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Deserializes one JSON object or returns a source-validation failure.</summary>
    public static SkillOperationResult<TBinding> Deserialize<TBinding> (string bindingJson, string hostName)
        where TBinding : class
    {
        if (string.IsNullOrWhiteSpace(bindingJson))
        {
            return Failure<TBinding>($"{hostName} agent binding must not be empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(bindingJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Failure<TBinding>($"{hostName} agent binding must be a JSON object.");
            }

            var propertyNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!propertyNames.Add(property.Name))
                {
                    return Failure<TBinding>($"{hostName} agent binding contains duplicate property '{property.Name}'.");
                }

                if (property.Value.ValueKind == JsonValueKind.Null)
                {
                    return Failure<TBinding>($"{hostName} agent binding property '{property.Name}' must not be null.");
                }
            }

            var binding = document.RootElement.Deserialize<TBinding>(SerializerOptions);
            return binding is null
                ? Failure<TBinding>($"{hostName} agent binding must be a JSON object.")
                : SkillOperationResult<TBinding>.Success(binding);
        }
        catch (JsonException)
        {
            return Failure<TBinding>($"{hostName} agent binding contains an invalid property or value.");
        }
        catch (NotSupportedException)
        {
            return Failure<TBinding>($"{hostName} agent binding contains an unsupported value.");
        }
    }

    private static SkillOperationResult<TBinding> Failure<TBinding> (string message)
    {
        return SkillOperationResult<TBinding>.FailureResult(SkillFailureCodes.SourceInvalid, message);
    }
}

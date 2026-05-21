
using System.Text.Json;

namespace MackySoft.Tests;

internal readonly struct JsonAssertionContext
{
    public JsonAssertionContext (JsonElement value, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("JSON path must not be null or whitespace.", nameof(path));
        }

        Value = value;
        Path = path;
    }

    public JsonElement Value { get; }

    public string Path { get; }
}

using System.Security.Cryptography;

namespace MackySoft.AgentSkills.Digests;

/// <summary> Formats and validates lowercase hexadecimal SHA-256 digest text. </summary>
internal static class Sha256LowerHex
{
    /// <summary> Gets the SHA-256 digest length in bytes. </summary>
    internal const int ByteCount = 32;

    /// <summary> Gets the SHA-256 digest length in hexadecimal characters. </summary>
    internal const int HexCharCount = ByteCount * 2;

    private const string HexChars = "0123456789abcdef";

    /// <summary> Computes a lowercase hexadecimal SHA-256 digest from source bytes. </summary>
    /// <param name="bytes"> The source bytes. </param>
    /// <returns> The lowercase hexadecimal SHA-256 digest. </returns>
    internal static string Compute (ReadOnlySpan<byte> bytes)
    {
        Span<byte> hashBytes = stackalloc byte[ByteCount];
        if (!SHA256.TryHashData(bytes, hashBytes, out var bytesWritten) || bytesWritten != ByteCount)
        {
            throw new InvalidOperationException("SHA-256 hash computation failed.");
        }

        return ToLowerHex(hashBytes);
    }

    /// <summary> Completes an incremental SHA-256 hash and returns its lowercase hexadecimal digest. </summary>
    /// <param name="hash"> The incremental SHA-256 hash. </param>
    /// <returns> The lowercase hexadecimal SHA-256 digest. </returns>
    internal static string GetHashAndReset (IncrementalHash hash)
    {
        ArgumentNullException.ThrowIfNull(hash);

        Span<byte> hashBytes = stackalloc byte[ByteCount];
        if (!hash.TryGetHashAndReset(hashBytes, out var bytesWritten) || bytesWritten != ByteCount)
        {
            throw new InvalidOperationException("SHA-256 hash computation failed.");
        }

        return ToLowerHex(hashBytes);
    }

    /// <summary> Converts digest bytes to lowercase hexadecimal text. </summary>
    /// <param name="bytes"> The digest bytes. </param>
    /// <returns> The lowercase hexadecimal text. </returns>
    internal static string ToLowerHex (ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteCount)
        {
            throw new ArgumentException("Digest byte count must match SHA-256 length.", nameof(bytes));
        }

        Span<char> chars = stackalloc char[HexCharCount];
        var index = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            var value = bytes[i];
            chars[index] = HexChars[value >> 4];
            chars[index + 1] = HexChars[value & 0x0F];
            index += 2;
        }

        return new string(chars);
    }

    /// <summary> Returns whether the value is a lowercase hexadecimal SHA-256 digest. </summary>
    /// <param name="value"> The digest text. </param>
    /// <returns> <see langword="true" /> when the value is valid; otherwise, <see langword="false" />. </returns>
    internal static bool IsDigestText (string? value)
    {
        return value is not null
            && value.Length == HexCharCount
            && value.AsSpan().IndexOfAnyExcept(HexChars) < 0;
    }
}

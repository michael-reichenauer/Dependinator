using System.IO;
using System.Text;

namespace Shared;

public sealed record CloudUserInfo(string UserId, string? Email);

public sealed record CloudAuthState(bool IsAvailable, bool IsAuthenticated, CloudUserInfo? User);

public sealed record CloudSyncQuota(long UsedBytes, long MaxBytes);

public sealed record CloudModelMetadata(
    string ModelKey,
    string NormalizedPath,
    DateTimeOffset UpdatedUtc,
    string ContentHash,
    long CompressedSizeBytes
);

public sealed record CloudModelList(IReadOnlyList<CloudModelMetadata> Models);

public sealed record CloudModelDocument(
    string ModelKey,
    string NormalizedPath,
    DateTimeOffset UpdatedUtc,
    string ContentHash,
    long CompressedSizeBytes,
    string CompressedContentBase64
);

public static class CloudModelPath
{
    public static string Normalize(string modelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        string normalizedPath = modelPath.Trim().Replace('\\', '/');
        while (normalizedPath.Contains("//", StringComparison.Ordinal))
            normalizedPath = normalizedPath.Replace("//", "/", StringComparison.Ordinal);

        return Path.GetFileName(normalizedPath);
    }

    public static string CreateKey(string modelPath)
    {
        string normalizedPath = Normalize(modelPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
            throw new ArgumentException("Model path must include a file name.", nameof(modelPath));

        StringBuilder keyBuilder = new(normalizedPath.Length);
        foreach (char character in normalizedPath)
        {
            if (char.IsWhiteSpace(character))
            {
                keyBuilder.Append('-');
            }
            else if (character is '<' or '>' or ':' or '\"' or '/' or '\\' or '|' or '?' or '*' or '\0')
            {
                keyBuilder.Append('-');
            }
            else
            {
                keyBuilder.Append(character);
            }
        }

        string key = keyBuilder.ToString().Trim('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Model path does not produce a valid cloud key.", nameof(modelPath));

        return key;
    }
}

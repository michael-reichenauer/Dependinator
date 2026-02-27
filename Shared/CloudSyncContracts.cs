using System.Security.Cryptography;
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

        return normalizedPath;
    }

    public static string CreateKey(string modelPath)
    {
        string normalizedPath = Normalize(modelPath);
        byte[] normalizedBytes = Encoding.UTF8.GetBytes(normalizedPath);
        byte[] hashBytes = SHA256.HashData(normalizedBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

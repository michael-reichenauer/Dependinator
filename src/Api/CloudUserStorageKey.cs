using System.Security.Cryptography;
using System.Text;
using Shared;

namespace Api;

internal static class CloudUserStorageKey
{
    public static string Create(CloudUserInfo user)
    {
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            string normalizedEmail = user.Email.Trim().ToLowerInvariant();
            return $"email-{BlobNameSanitizer.SanitizeForBlobName(normalizedEmail)}";
        }

        return BlobNameSanitizer.SanitizeForBlobName(user.UserId, fallbackValue: "unknown-user");
    }

    static string ComputeHash(string value)
    {
        byte[] valueBytes = Encoding.UTF8.GetBytes(value);
        byte[] hashBytes = SHA256.HashData(valueBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

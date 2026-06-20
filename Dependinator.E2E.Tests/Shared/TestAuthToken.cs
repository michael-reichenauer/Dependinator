using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Dependinator.E2E.Tests.Shared;

// Mints RS256 JWTs for cloud-sync e2e tests, signed with the throwaway test key in
// TestAuth/private-key.pem. The matching public key is served by TestAuth/jwks-server.js
// (started by ./e2e -s), so the running Functions host validates these like real tokens.
//
// The issuer must match the Functions host's CloudSync__ClerkIssuer; ./e2e -s sets both
// from E2E_CLERK_ISSUER. The 'kid' must match the one in TestAuth/jwks.json.
public static class TestAuthToken
{
    public const string DefaultIssuer = "http://127.0.0.1:7072";
    const string KeyId = "e2e-test-key";

    public static string Issuer => Environment.GetEnvironmentVariable("E2E_CLERK_ISSUER") ?? DefaultIssuer;

    public static string Create(string sub = "e2e-test-user", string email = "e2e@dependinator.test")
    {
        RSA rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(PrivateKeyPath()));

        RsaSecurityKey securityKey = new(rsa) { KeyId = KeyId };
        SigningCredentials credentials = new(securityKey, SecurityAlgorithms.RsaSha256);

        DateTime now = DateTime.UtcNow;
        JwtSecurityToken token = new(
            issuer: Issuer,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, sub), new Claim(JwtRegisteredClaimNames.Email, email)],
            notBefore: now,
            expires: now.AddHours(1),
            signingCredentials: credentials
        );
        token.Payload[JwtRegisteredClaimNames.Iat] = new DateTimeOffset(now).ToUnixTimeSeconds();

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    static string PrivateKeyPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "TestAuth", "private-key.pem");
    }
}

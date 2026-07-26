# Test auth keys (e2e cloud sync)

These files let the e2e sync tests authenticate **without Clerk**, fully offline.

- `private-key.pem` — RSA private key the C# test uses to sign JWTs (`TestAuthToken`).
- `jwks.json` — the matching public key, served by `jwks-server.js` at
  `/.well-known/jwks.json`. `./scripts/e2e -s` starts that server and points the Functions
  host's `CloudSync__ClerkIssuer` at it, so `CloudSyncBearerTokenValidator` fetches
  this key and validates the minted token like a real one.
- `jwks-server.js` — the standalone static server (started by `./scripts/e2e -s`).

This is a **throwaway test-only keypair** — it never signs anything real and is
deliberately committed so the suite runs with no setup or secrets. The `kid`
(`e2e-test-key`) must match between `jwks.json` and `TestAuthToken`.

## Regenerate

```bash
node Dependinator.E2E.Tests/TestAuth/generate-keys.js
```

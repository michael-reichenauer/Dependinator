// Regenerates the throwaway test keypair used by the e2e cloud-sync tests.
// Writes private-key.pem (signing) and jwks.json (public, served to the API).
// Run: node Dependinator.E2E.Tests/TestAuth/generate-keys.js
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");

const kid = "e2e-test-key";
const dir = __dirname;

const { publicKey, privateKey } = crypto.generateKeyPairSync("rsa", {
    modulusLength: 2048,
});

fs.writeFileSync(path.join(dir, "private-key.pem"), privateKey.export({ type: "pkcs8", format: "pem" }));

const jwk = publicKey.export({ format: "jwk" });
const jwks = {
    keys: [{ kty: jwk.kty, n: jwk.n, e: jwk.e, kid, use: "sig", alg: "RS256" }],
};
fs.writeFileSync(path.join(dir, "jwks.json"), JSON.stringify(jwks, null, 2) + "\n");

console.log("Wrote private-key.pem and jwks.json (kid=" + kid + ")");

// Minimal static JWKS server for e2e cloud-sync tests.
//
// Serves the committed test public key at /.well-known/jwks.json so the running
// Azure Functions host (CloudSyncBearerTokenValidator) can validate JWTs that
// the C# test mints with the matching private key — no real Clerk needed.
//
// Started by `./scripts/e2e -s`. Port via argv[2] or PORT env (default 7072).
const http = require("http");
const fs = require("fs");
const path = require("path");

const port = Number(process.argv[2] || process.env.PORT || 7072);
const jwks = fs.readFileSync(path.join(__dirname, "jwks.json"), "utf8");

const server = http.createServer((req, res) => {
    if (req.url === "/.well-known/jwks.json") {
        res.writeHead(200, { "Content-Type": "application/json" });
        res.end(jwks);
        return;
    }
    res.writeHead(404, { "Content-Type": "text/plain" });
    res.end("Not found");
});

server.listen(port, "127.0.0.1", () => {
    console.log(`Test JWKS server on http://127.0.0.1:${port}/.well-known/jwks.json`);
});

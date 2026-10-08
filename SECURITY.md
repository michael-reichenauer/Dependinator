# Security policy

## Supported versions

Only the latest release of the VS Code extension and the current web app at
[dependinator.com](https://dependinator.com) receive fixes.

## Reporting a vulnerability

Please do not open a public issue for security problems.

Use GitHub's private vulnerability reporting instead: go to the
[Security tab](https://github.com/michael-reichenauer/Dependinator/security/advisories/new)
and choose "Report a vulnerability". You will get a response within a few days,
and a fix or mitigation is published as soon as it is ready.

## Scope

- The VS Code extension (`src/DependinatorVsCode/`) and the bundled language server
- The web app and the cloud-sync API (`src/Dependinator.Wasm/`, `src/Api/`)

Cloud sync is optional. Without signing in, Dependinator keeps all data local.

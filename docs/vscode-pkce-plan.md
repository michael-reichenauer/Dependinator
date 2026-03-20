# VS Code PKCE Migration Plan

## Goal

Replace the current VS Code extension cloud-sync login flow based on device code with an authorization code flow using PKCE.

Desired outcome:

- browser-hosted `Dependinator.Wasm` keeps its current SWA auth flow
- VS Code-hosted `Dependinator.Wasm` signs in through the extension host without the extra device-code copy/paste step
- user only completes the normal Entra External ID browser sign-in flow, including the email OTP step

## Why Change

Current VS Code login uses device code flow.

That works, but it adds an extra step:

1. extension shows a device code
2. user copies/pastes or confirms it in the browser
3. user then completes the normal Entra email login flow

For a desktop/native app, authorization code flow with PKCE is a better fit and removes the extra device-code step.

## Constraints

- The current implementation works in all target hosts:
  - browser/SWA
  - VS Code extension host
- Do not regress the current browser-hosted auth flow.
- Keep API auth unchanged if possible:
  - API should still accept bearer tokens from the VS Code extension
  - SWA/browser path should still use `x-ms-client-principal`
- Prefer a system-browser flow, not an embedded browser.

## Current Architecture

Current VS Code flow:

1. WASM webview sends `cloudSync/request`
2. extension host handles login/API calls in `DependinatorVsCode/src/cloudSyncNode.ts`
3. extension host uses device code flow
4. extension host stores the access token in VS Code secret storage
5. extension host calls the deployed `/api/...` endpoints with `X-Dependinator-Authorization`

Relevant files:

- [cloudSyncNode.ts](/workspaces/Dependinator/DependinatorVsCode/src/cloudSyncNode.ts)
- [extension.ts](/workspaces/Dependinator/DependinatorVsCode/src/extension.ts)
- [CloudSyncBearerTokenValidator.cs](/workspaces/Dependinator/Api/CloudSyncBearerTokenValidator.cs)
- [CloudSyncUserProvider.cs](/workspaces/Dependinator/Api/CloudSyncUserProvider.cs)

## Target Architecture

Target VS Code flow:

1. webview requests `login`
2. extension host starts auth code flow with PKCE in the system browser
3. Entra redirects back to a loopback redirect URI on localhost
4. extension host redeems the auth code with `code_verifier`
5. extension host stores the access token
6. extension host calls the API as today using the bearer-token path

The browser/SWA host remains unchanged.

## Entra App Registration Changes

Use the existing VS Code public-client app registration if practical.

Required changes:

1. Add a native/desktop redirect URI:
   - `http://localhost`
   - or a fixed localhost port if the chosen client library requires it
2. Keep the app as a public client.
3. Keep the exposed API scope:
   - `api://<client-id>/access_as_user`

Important:

- No client secret should be introduced for the VS Code extension app.
- If library behavior requires a different redirect URI format, update the registration to match exactly.

## Recommended Implementation Approach

### Option A: Use `@azure/msal-node`

Recommended first choice.

Why:

- supported auth library
- built for native/public client scenarios
- avoids hand-rolling PKCE/token handling

Likely approach:

1. add `@azure/msal-node` dependency to `DependinatorVsCode`
2. create a `PublicClientApplication`
3. use system-browser auth code flow with PKCE and loopback redirect
4. request scopes:
   - `openid`
   - `profile`
   - `email`
   - `offline_access`
   - `api://<client-id>/access_as_user`

Open question for implementation session:

- whether the chosen MSAL API for Node fits cleanly in the VS Code extension host without introducing unnecessary complexity

### Option B: Implement raw auth code + PKCE manually

Fallback only.

Why not preferred:

- more error-prone
- more token/security protocol code to own
- more maintenance risk

## Proposed Work Breakdown

### Phase 1: Registration and Configuration

1. Confirm the VS Code app registration includes:
   - public client enabled
   - exposed API scope `access_as_user`
   - localhost redirect URI for native/system-browser auth
2. Keep existing production defaults:
   - base URL
   - OIDC metadata URL
   - client ID

### Phase 2: Extension-Host Auth Refactor

1. Replace the device authorization start/poll logic in `cloudSyncNode.ts`
2. Add PKCE auth start + callback handling
3. Keep token storage in VS Code secret storage
4. Keep the rest of the cloud-sync request handling unchanged

Expected code areas:

- [cloudSyncNode.ts](/workspaces/Dependinator/DependinatorVsCode/src/cloudSyncNode.ts)
- possibly [package.json](/workspaces/Dependinator/DependinatorVsCode/package.json) if new dependency/build settings are needed

### Phase 3: UX Cleanup

1. Remove device-code specific prompts:
   - `Copy Code`
   - device-code instructions
2. Replace with a simpler browser login status message if needed:
   - `Opening browser for Dependinator sign-in...`
3. Keep current menu/UI behavior in WASM unchanged

### Phase 4: Validation

Validate all hosts:

1. browser-hosted WASM
   - login still works
   - sync still works
2. VS Code-hosted WASM
   - login opens browser directly
   - no manual device-code entry step
   - sync still works
3. local `run-sync`
   - if local Entra testing is configured, verify loopback callback flow locally

## Risks

1. Loopback redirect handling in VS Code extension host
- requires opening a localhost listener and closing it reliably

2. Library/runtime fit
- the chosen auth library must work cleanly in the Node-based extension host

3. External ID redirect URI specifics
- the registered redirect URI must exactly match the implementation

4. Token scope/audience regression
- keep requesting `api://<client-id>/access_as_user`
- do not regress the bearer-token API path that now works

## Suggested Acceptance Criteria

1. VS Code login opens the browser directly without a device-code prompt
2. user completes only the normal Entra sign-in flow
3. extension obtains a valid access token for the API scope
4. `/api/auth/me` returns authenticated for the VS Code bearer-token path
5. browser-hosted SWA login remains unchanged and working

## Notes For Future Session

- Keep the custom `X-Dependinator-Authorization` header for the VS Code API path unless there is a clear reason to change it.
- Do not remove the working bearer-token API validation path.
- Do not mix the VS Code native login flow with the browser/SWA session flow; they are intentionally separate.
- Prefer using a supported auth library rather than hand-rolling the PKCE flow.

## Sources

- Microsoft identity platform auth code flow:
  - https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow
- MSAL authentication flow support:
  - https://learn.microsoft.com/en-us/entra/msal/msal-authentication-flows
- MSAL.NET system browser guidance for native apps:
  - https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/using-web-browsers
- Redirect URI guidance:
  - https://learn.microsoft.com/en-us/entra/identity-platform/how-to-add-redirect-uri

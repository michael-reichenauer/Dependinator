# Cloud Sync Plan

## Goal

Add optional cloud sync for cached `ModelDto` data in `Dependinator.Wasm` so the same model can be used across devices.

The existing local browser cache remains in place. Cloud sync is an extra manual action in v1.

## Confirmed Decisions

- Auth provider: Microsoft Entra External ID
- SWA custom OIDC provider name: `entraExternalId`
- Hosting plan: Azure Static Web Apps Standard
- API runtime: Azure Functions isolated worker on `net8.0` or `net9.0`
- Storage: Azure Blob Storage
- Synced data: `ModelDto` only
- Model identity: normalized path hashed with SHA-256
- Conflict policy: last write wins
- Quota target: about 10 MB compressed data per user
- v1 sync mode: manual only
- v1 pull mode: manual only
- v1 offline behavior: keep writing locally; sync only when user clicks sync while online
- v1 cloud retention: only the latest cloud copy per model
- v1 target host: `Dependinator.Wasm` only

## User Experience v1

1. User signs in with Entra External ID using email OTP.
2. User opens a model as usual.
3. Local cache continues to save to IndexedDB as it does today.
4. User can click `Sync Up` to upload the current cached model to the cloud.
5. User can click `Sync Down` to download the cloud copy and replace the local cached copy.
6. `Sync Down` must prompt before overwriting local cached data.

## Current Local Behavior

- The local cache is stored in IndexedDB through `Database`.
- `ModelService` reads cached model data first and then refreshes by parsing source.
- This behavior should stay unchanged in v1.

## Proposed API Shape

Base path:

- `/api/auth/me`
- `/api/models/{modelKey}`

Endpoints:

- `GET /api/auth/me`
  - Returns signed-in user info needed by the UI.
- `PUT /api/models/{modelKey}`
  - Uploads the compressed serialized `ModelDto`.
  - Overwrites the previous version.
- `GET /api/models/{modelKey}`
  - Returns the latest stored model for that user and model key.

Optional later:

- `DELETE /api/models/{modelKey}`
- `GET /api/models`

## Storage Shape

Recommended blob path:

- `users/{userId}/models/{modelKey}.json.gz`

Recommended metadata:

- `normalizedPath`
- `updatedUtc`
- `contentHash`
- `compressedSizeBytes`

## Client Architecture

Add a cloud sync abstraction in shared UI code so host-specific transport can vary later.

Suggested interface shape:

- `ICloudSyncService`
  - `Task<R<UserInfo>> GetCurrentUserAsync()`
  - `Task<R<CloudModelInfo>> PushAsync(string modelPath, ModelDto modelDto)`
  - `Task<R<ModelDto>> PullAsync(string modelPath)`

Suggested implementations:

- `HttpCloudSyncService`
  - Used by standalone `Dependinator.Wasm`
  - Talks directly to the SWA API
- `NoCloudSyncService`
  - Used by hosts where sync is unavailable

## Future VS Code Direction

This is important for later sessions.

When the UI runs inside the VS Code webview, the WASM UI should not call the cloud API directly.

Instead:

- the webview UI should call the same sync abstraction
- the VS Code host should provide a transport implementation that forwards sync requests to the extension host
- the extension host should own the actual network communication with the API
- the LSP may help prepare or transform data if useful, but it should not be the default owner of API communication

In other words:

- standalone browser host -> direct HTTP to SWA API
- VS Code webview host -> RPC/message bridge to extension host -> HTTP to API

This keeps the UI host-agnostic and avoids coupling cloud sync to the browser sandbox limitations inside VS Code.

## Why This Boundary Matters

- The standalone WASM app can use SWA auth and API calls directly.
- The VS Code webview host may not have the same network or auth capabilities.
- The extension host is a better place to manage future API communication and auth/token handling.
- Keeping sync behind an interface prevents rework when VS Code sync is added later.

## Suggested Implementation Phases

### Phase 1: API Foundation

- Move `Api` to `net8.0` or `net9.0`
- Add blob storage service
- Add authenticated endpoints
- Add per-user quota check

### Phase 2: WASM Sync Client

- Add cloud sync DTOs
- Add model key normalization and hashing
- Add `HttpCloudSyncService`
- Add sync commands for push and pull

### Phase 3: UI

- Add login and logout controls
- Add signed-in status display
- Add `Sync Up` and `Sync Down` actions
- Add overwrite confirmation for `Sync Down`

### Phase 4: Deployment

- Enable API deployment in SWA workflow
- Add SWA auth configuration for Entra External ID
- Add blob storage configuration

### Phase 5: Local Development

- Add a local script to run SWA, Functions, and Azurite together if practical
- If full auth emulation is limited, keep local API and storage testable and use deployed auth for end-to-end validation

### Phase 6: Future VS Code Sync

- Add an RPC-backed sync implementation for the VS Code webview host
- Route API calls through the extension host
- Reuse the same API contract and `ICloudSyncService` interface

## Notes For Future Sessions

- Do not couple cloud sync directly to IndexedDB code.
- Do not make the VS Code webview call the API directly.
- Prefer adding a sync abstraction before adding UI buttons.
- Preserve current local-first caching behavior in `ModelService`.
- Keep v1 limited to manual sync and single latest cloud copy.

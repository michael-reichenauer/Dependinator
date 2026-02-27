# Dependinator

Dependinator is a tool for visualizing and exploring software dependencies. This repo contains the shared UI and core logic plus multiple hosts (Blazor Server, Blazor WebAssembly) and an Azure Functions API used for cloud sync.

## Solution structure
- `Dependinator/`: Shared UI, parsing, models, utilities.
- `DependinatorWeb/`: Blazor Server host for local development.
- `DependinatorWasm/`: Blazor WebAssembly host (Azure Static Web Apps target).
- `Api/`: Azure Functions API for cloud sync.
- `Shared/`: Shared DTOs/models between client and API.
- `Dependinator.Tests/`: xUnit tests.

## Quick start
- Build: `./build`
- Run server (live dev): `./watch`
- Run WASM sample: `./run`
- Run WASM + API + Azurite locally: `./run-sync`
- Tests: `dotnet test Dependinator.Tests/Dependinator.Tests.csproj`

## Azure Static Web Apps (optional)
- See `swa-cli.config.json` for the `dependinator-test` configuration.
- Cloud sync auth uses a custom OpenID Connect provider named `entraExternalId`.
- Required Static Web App application settings:
  - `ENTRA_EXTERNAL_ID_CLIENT_ID`
  - `ENTRA_EXTERNAL_ID_CLIENT_SECRET`
  - `CloudSync__ContainerName`
  - `CloudSync__MaxUserQuotaBytes`
  - `CloudSync__StorageConnectionString`
  - `CloudSync__OpenIdConfigurationUrl`
  - `CloudSync__BearerAudience`
- Required callback URL in the identity provider registration:
  - `https://<your-site>/.auth/login/entraExternalId/callback`
- VS Code extension-host sync uses separate extension settings:
  - `dependinator.cloudSync.baseUrl`
  - `dependinator.cloudSync.openIdConfigurationUrl`
  - `dependinator.cloudSync.clientId`

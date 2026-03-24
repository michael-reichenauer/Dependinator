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
- Run server with cloud sync: `./watch-sync`
- Run WASM sample: `./run`
- Run WASM + API + Azurite locally: `./run-sync`
- Tests: `dotnet test Dependinator.Tests/Dependinator.Tests.csproj`

## Cloud sync

Cloud sync uses [Clerk](https://clerk.com) for authentication (magic links / email OTP). The API validates Clerk-issued JWTs via JWKS.

### Azure Static Web Apps deployment

- See `swa-cli.config.json` for the `dependinator-test` configuration.
- Required Static Web App application settings:
  - `CloudSync__ClerkIssuer`
  - `CloudSync__ContainerName`
  - `CloudSync__MaxUserQuotaBytes`
  - `CloudSync__StorageConnectionString`

### VS Code extension

- The `dependinator.cloudSync.baseUrl` setting controls which API endpoint the extension uses.
- The extension serves a local Clerk sign-in page and stores the session JWT in VS Code secrets.

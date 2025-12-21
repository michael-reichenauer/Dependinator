# Dependinator

Dependinator is a tool for visualizing and exploring software dependencies. This repo contains the shared UI and core logic plus multiple hosts (Blazor Server, Blazor WebAssembly) and a sample Azure Functions API.

## Solution structure
- `Dependinator/`: Shared UI, parsing, models, utilities.
- `DependinatorWeb/`: Blazor Server host for local development.
- `DependinatorWasm/`: Blazor WebAssembly host (Azure Static Web Apps target).
- `Api/`: Azure Functions sample (planned for future use).
- `Shared/`: Shared DTOs/models between client and API.
- `Dependinator.Tests/`: xUnit tests.

## Quick start
- Build: `./build`
- Run server (live dev): `./watch`
- Run WASM sample: `./run`
- Tests: `dotnet test Dependinator.Tests/Dependinator.Tests.csproj`

## Azure Static Web Apps (optional)
- See `swa-cli.config.json` for the `dependinator-test` configuration.

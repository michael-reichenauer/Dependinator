# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Dependinator is a tool for visualizing and exploring software dependencies. It has multiple hosts (Blazor Server, Blazor WebAssembly), an Azure Functions API for cloud sync, a Language Server Protocol (LSP) server, and a VS Code extension.

## Solution Structure

The active solution is `Dependinator.sln` targeting `net10.0` (SDK pinned in `global.json`).

**Runtime/host projects:**
- `Dependinator.Web/` — Blazor Server host for local development
- `Dependinator.Wasm/` — Blazor WebAssembly host (Azure Static Web Apps target) and Web UI for VSCode Extension 
- `Api/` — Azure Functions host for cloud sync
- `Dependinator.Lsp/` — LSP server executable

**Core libraries:**
- `Dependinator/` — shared UI, `App/`, `Diagrams/`, `Modeling/` components
- `Dependinator.Core/` — core parsing/domain logic/utilities (`Parsing/`, `Models/`, `Utils/`)
- `Dependinator.Roslyn/` — Roslyn-based parsing (`Parsing/`)
- `Shared/` — shared DTOs/models between client and API

**Test projects:** `Api.Tests/`, `Dependinator.Tests/`, `Dependinator.Core.Tests/`, `Dependinator.Roslyn.Tests/`

**VS Code extension (not part of `Dependinator.sln`):**
- `DependinatorVsCode/` — TypeScript extension packaging the web UI + language server (`src/extension.ts`, `src/webview.ts`, `src/languageServer.ts`, `src/cloudSyncNode.ts`)

## Build & Development Commands

```bash
# Build
./build                                        # builds Dependinator.Web
dotnet build Dependinator.sln

# Run
./watch                                        # dotnet watch for Dependinator.Web (live dev)
./run                                          # publish Wasm in Release + serve via SWA CLI
./run-sync                                     # Wasm + Azurite + Functions host + SWA CLI

# Test
dotnet test Dependinator.sln                   # all tests
dotnet test Dependinator.Tests/Dependinator.Tests.csproj
dotnet test Dependinator.Core.Tests/Dependinator.Core.Tests.csproj
dotnet test Dependinator.Roslyn.Tests/Dependinator.Roslyn.Tests.csproj
dotnet test Api.Tests/Api.Tests.csproj

# VS Code extension
npm install --prefix ./DependinatorVsCode
npm run compile --prefix ./DependinatorVsCode
npm run package --prefix ./DependinatorVsCode
npm run install:vsix --prefix ./DependinatorVsCode
./build-ext                                    # npm install + version bump + package

# Packages
./updatepackages -u                            # upgrade non-major packages
./updatepackages -m                            # allow major upgrades
dotnet list Dependinator.sln package --outdated
dotnet list Dependinator.sln package --vulnerable
```

`./run-sync` requires `func` (Azure Functions Core Tools), `azurite`, and `swa` (SWA CLI) to be installed.

## Tooling & Conventions

- **Package versions:** centrally managed in `Directory.Packages.props`; avoid per-project version overrides.
- **Formatting:** CSharpier (`.csharpierrc.json`) + `.editorconfig`. Run `dotnet csharpier --check .` to verify; `CSharpier.MsBuild` also enforces formatting during build.
- **Style:** 4-space indent, explicit types over `var`, PascalCase for types/methods/properties/constants, nullable-aware code, braces preferred.
- **Tests:** xUnit + Moq + Verify.Xunit. Name tests as `MethodName_ShouldDoX()`. `Dependinator.Core.Tests/Parsing/Solutions/SolutionParserTests.cs` resolves `Dependinator.sln` dynamically — do not hardcode `/workspaces/...` paths in tests.

## Architecture Notes

**Parsing pipeline:** `Dependinator.Core/Parsing/` orchestrates via `ParserService`. Roslyn-specific logic lives in `Dependinator.Roslyn/Parsing/`.

**Cloud sync — two auth paths:**
- Browser/SWA: SWA session auth via `/.auth/...` and `x-ms-client-principal`
- VS Code extension host: bearer-token auth via `X-Dependinator-Authorization` header

**Wasm cloud sync routing:** the browser-hosted `Dependinator.Wasm` calls `/api` directly; the VS Code-hosted `Dependinator.Wasm` routes cloud sync through the extension host (`DependinatorVsCode/src/cloudSyncNode.ts`), not through the LSP.

**VS Code extension:** when editing functionality used by the extension, check `DependinatorVsCode/scripts/` for corresponding build steps.

## Commit Style

Imperative, concise subject under 72 chars (e.g., `Fix parsing of generic constraints`). Include rationale in commit body when non-obvious. Link issues with `Fixes #123` in PRs.

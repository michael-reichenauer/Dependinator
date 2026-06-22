# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Dependinator is a tool for visualizing and exploring software dependencies. It has multiple hosts (Blazor Server, Blazor WebAssembly), an Azure Functions API for cloud sync, a Language Server Protocol (LSP) server, and a VS Code extension.

## Solution Structure

The active solution is `Dependinator.sln` targeting `net10.0` (SDK pinned in `global.json`). The solution file stays at the repo root; all project folders live under `src/`.

**Runtime/host projects:**
- `src/Dependinator.Web/` — Blazor Server host for local development
- `src/Dependinator.Wasm/` — Blazor WebAssembly host (Azure Static Web Apps target) and Web UI for VSCode Extension 
- `src/Api/` — Azure Functions host for cloud sync
- `src/Dependinator.Lsp/` — LSP server executable

**Core libraries:**
- `src/Dependinator.UI/` — shared UI, `App/`, `Diagrams/`, `Modeling/` components
- `src/Dependinator.Core/` — core parsing/domain logic/utilities (`Parsing/`, `Models/`, `Utils/`)
- `src/Dependinator.Roslyn/` — Roslyn-based parsing (`Parsing/`)
- `src/Shared/` — shared DTOs/models between client and API

**Test projects:** `src/Api.Tests/`, `src/Dependinator.UI.Tests/`, `src/Dependinator.Core.Tests/`, `src/Dependinator.Roslyn.Tests/`

**VS Code extension (not part of `Dependinator.sln`):**
- `src/DependinatorVsCode/` — TypeScript extension packaging the web UI + language server (`src/extension.ts`, `src/webview.ts`, `src/languageServer.ts`, `src/cloudSyncNode.ts`)

## Build & Development Commands

```bash
# Build
./build                                        # builds Dependinator.Web
dotnet build Dependinator.sln

# Run
./watch                                        # dotnet watch for Dependinator.Web + Azurite + Functions host (live dev)
./run                                          # publish Wasm in Release + Azurite + Functions host + SWA CLI

# Test
./test                                         # unit tests, then e2e (args forwarded to ./e2e, e.g. ./test -s -t)
dotnet test Dependinator.sln                   # all unit tests (e2e are skipped)
dotnet test src/Dependinator.UI.Tests/Dependinator.UI.Tests.csproj
dotnet test src/Dependinator.Core.Tests/Dependinator.Core.Tests.csproj
dotnet test src/Dependinator.Roslyn.Tests/Dependinator.Roslyn.Tests.csproj
dotnet test src/Api.Tests/Api.Tests.csproj

# UI (Playwright) tests — see src/Dependinator.E2E.Tests/README.md
./e2e                                          # chromium; auto-starts app if not running
./e2e -b firefox                               # specific browser (chromium|firefox|webkit)
./e2e -a                                       # all three browsers (before releases)
./e2e -s                                       # also start Azurite + Functions for sync tests
./e2e -t                                       # record Playwright traces into ./src/Dependinator.E2E.Tests/traces
./trace [6|path.zip]                           # view a recorded trace (serves viewer on :9322)
# CI: .github/workflows/e2e.yml runs ./e2e (chromium) on PRs; uploads traces on failure.

# VS Code extension
npm install --prefix ./src/DependinatorVsCode
npm run compile --prefix ./src/DependinatorVsCode
npm run package --prefix ./src/DependinatorVsCode
npm run install:vsix --prefix ./src/DependinatorVsCode
./build-ext                                    # npm install + version bump + package

# Packages
./updatepackages -u                            # upgrade non-major packages
./updatepackages -m                            # allow major upgrades
dotnet list Dependinator.sln package --outdated
dotnet list Dependinator.sln package --vulnerable
```

`./run` requires `func` (Azure Functions Core Tools), `azurite`, and `swa` (SWA CLI) to be installed.
`./watch` requires `func` and `azurite` (no SWA CLI needed).

## Tooling & Conventions

- **Package versions:** centrally managed in `Directory.Packages.props`; avoid per-project version overrides.
- **Formatting:** CSharpier (`.csharpierrc.json`) + `.editorconfig`. Run `dotnet csharpier --check .` to verify; `CSharpier.MsBuild` also enforces formatting during build.
- **Style:** 4-space indent, explicit types over `var`, PascalCase for types/methods/properties/constants, nullable-aware code, braces preferred.
- **Tests:** xUnit + Moq + Verify.Xunit. Name tests as `MethodName_ShouldDoX()`. `src/Dependinator.Core.Tests/Parsing/Solutions/SolutionParserTests.cs` resolves `Dependinator.sln` (at the repo root) dynamically via `src/Dependinator.Core.Tests/Root.cs` — do not hardcode `/workspaces/...` paths in tests.
- **UI/e2e tests:** `src/Dependinator.E2E.Tests/` (Microsoft.Playwright + xUnit) run only via `./e2e` (or `E2E=1`); they target the running app at `http://localhost:5000`. Keep the `Microsoft.Playwright.Xunit` version in `Directory.Packages.props` in sync with `PLAYWRIGHT_VERSION` in `.devcontainer/post-create.sh`.
- **Browser checks:** after UI changes, use the `ui-check` skill (`.claude/skills/ui-check/`) to verify behavior with `playwright-cli` and run `./e2e`.

## Architecture Notes

**Parsing pipeline:** `src/Dependinator.Core/Parsing/` orchestrates via `ParserService`. Roslyn-specific logic lives in `src/Dependinator.Roslyn/Parsing/`.

**Cloud sync auth:** All hosts use Clerk for authentication. The API validates Clerk-issued JWTs via JWKS. Browser hosts (Blazor Server and WASM) use Clerk.js for sign-in and attach Bearer tokens to API requests. The VS Code extension serves a local sign-in page with Clerk.js and stores the JWT in VS Code secrets. All API calls use Bearer tokens via the `Authorization` or `X-Dependinator-Authorization` header.

**Wasm cloud sync routing:** the browser-hosted `Dependinator.Wasm` calls `/api` directly; the VS Code-hosted `Dependinator.Wasm` routes cloud sync through the extension host (`DependinatorVsCode/src/cloudSyncNode.ts`), not through the LSP.

**VS Code extension cloud sync:** the extension serves a self-contained Clerk sign-in page from a local HTTP callback server. The `dependinator.cloudSync.baseUrl` setting controls which API endpoint is used (production, SWA CLI, or Functions direct).

**VS Code extension:** when editing functionality used by the extension, check `src/DependinatorVsCode/scripts/` for corresponding build steps.

## Commit Style

Imperative, concise subject under 72 chars (e.g., `Fix parsing of generic constraints`). Include rationale in commit body when non-obvious. Link issues with `Fixes #123` in PRs.

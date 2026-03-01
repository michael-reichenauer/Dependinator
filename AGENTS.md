# Repository Guidelines

## Project Structure & Module Organization
- Solution: `Dependinator.sln` (main .NET projects, currently `net10.0` via `global.json`)
- Runtime/host projects:
  - `Dependinator.Web/` (Blazor Server host for local development)
  - `Dependinator.Wasm/` (Blazor WebAssembly host / Static Web Apps target)
  - `Api/` (Azure Functions host for cloud sync)
  - `Dependinator.Lsp/` (LSP server executable)
- Core libraries:
  - `Dependinator/` (shared UI + app components)
  - `Dependinator.Core/` (core parsing/domain logic/utilities)
  - `Dependinator.Roslyn/` (Roslyn-based parsing/integration)
  - `Shared/` (shared DTOs/models)
- Test projects:
  - `Api.Tests/`
  - `Dependinator.Tests/`
  - `Dependinator.Core.Tests/`
  - `Dependinator.Roslyn.Tests/`
- VS Code extension (not part of `Dependinator.sln`):
  - `DependinatorVsCode/` (TypeScript extension packaging the web UI + language server)
- Common folders you will work in frequently:
  - `Dependinator/App/`, `Dependinator/Diagrams/`
  - `Dependinator.Core/Parsing/`, `Dependinator.Core/Models/`, `Dependinator.Core/Utils/`
  - `Dependinator.Roslyn/Parsing/`

Note:
- Legacy sibling folders with undotted names still exist in the repo (`DependinatorWeb/`, `DependinatorWasm/`, `DependinatorCore/`, `DependinatorRoslyn/`, `DependinatorLanguageServer/`), but the active solution/projects use the dotted names above.

## Build, Test, and Development Commands
- Root helper scripts:
  - `./build` (builds `Dependinator.Web`)
  - `./watch` (runs `dotnet watch` for `Dependinator.Web`)
  - `./run` (publishes `Dependinator.Wasm` in Release and serves `wwwroot` via SWA CLI)
  - `./run-sync` (publishes `Dependinator.Wasm`, starts Azurite, starts the Functions host on `7071`, then serves the app through SWA CLI)
  - `./updatepackages -u` (upgrade non-major packages) or `./updatepackages -m` (allow major)
  - `./build-ext` (npm install + version bump + VS Code extension package build)
- Standard .NET:
  - `dotnet restore`
  - `dotnet build Dependinator.sln`
  - `dotnet test Dependinator.sln`
  - `dotnet run --project Dependinator.Web/Dependinator.Web.csproj`
  - `dotnet run --project Dependinator.Lsp/Dependinator.Lsp.csproj`
- Targeted tests:
  - `dotnet test Api.Tests/Api.Tests.csproj`
  - `dotnet test Dependinator.Tests/Dependinator.Tests.csproj`
  - `dotnet test Dependinator.Core.Tests/Dependinator.Core.Tests.csproj`
  - `dotnet test Dependinator.Roslyn.Tests/Dependinator.Roslyn.Tests.csproj`
- VS Code extension (`DependinatorVsCode/`):
  - `npm install --prefix ./DependinatorVsCode`
  - `npm run compile --prefix ./DependinatorVsCode`
  - `npm run build:extension --prefix ./DependinatorVsCode`
  - `npm run package --prefix ./DependinatorVsCode`
  - `npm run install:vsix --prefix ./DependinatorVsCode`
- Packages and security:
  - `dotnet list Dependinator.sln package --outdated`
  - `dotnet list Dependinator.sln package --vulnerable`

## Environment & Tooling Notes
- SDK is pinned in `global.json` (`10.0.101`, roll-forward `latestFeature`).
- Package versions are managed centrally in `Directory.Packages.props`.
- CSharpier is used in two ways:
  - `csharpier` dotnet tool (CLI formatting/checks)
  - `CSharpier.MsBuild` package (formatting enforcement during build)
- `./run` uses `swa` if installed, otherwise falls back to `npx @azure/static-web-apps-cli`.
- `./run-sync` requires:
  - Azure Functions Core Tools (`func`)
  - Azurite (`azurite`, or `npx azurite` as fallback)
  - SWA CLI (`swa`, or `npx @azure/static-web-apps-cli` as fallback)
- `.devcontainer/Dockerfile` now installs Azure Functions Core Tools for local API runs in the devcontainer.
- Some helper scripts are convenience wrappers; prefer explicit `dotnet`/`npm` commands when debugging CI or script issues.
- `install-ext` currently references `./build-vscode` (not present in repo); prefer `npm run package/install:vsix --prefix ./DependinatorVsCode` until that wrapper is fixed.
- Local runtime artifacts are intentionally not committed:
  - `Api/local.settings.json`
  - `Dependinator.Wasm/publish/`
  - `.azurite/`

## Coding Style & Naming Conventions
- Formatting via `.editorconfig` and CSharpier (`.csharpierrc.json`).
- Indentation: 4 spaces; UTF-8 BOM; final newline enabled.
- `System` usings first; `this.` qualification is not required.
- Prefer explicit types over `var` by default (per `.editorconfig` `csharp_style_var_* = false`).
- Braces are preferred (`csharp_prefer_braces = true`).
- Naming: PascalCase for types/methods/properties and constants.
- Keep files focused; avoid one-letter identifiers; enable nullable-aware code patterns.

## CSharpier Formatting
- Tool vs package: `csharpier` (dotnet tool) is separate from `CSharpier.MsBuild` (NuGet that runs during builds). Versions do not need to match.
- Update tool: run `dotnet tool update csharpier` at repo root; this updates `.config/dotnet-tools.json`. To pin, use `--version X.Y.Z`. Teammates run `dotnet tool restore`.
- Update MSBuild package: edit `Directory.Packages.props` to set `<PackageVersion Include="CSharpier.MsBuild" Version="X.Y.Z" />`. Use central management; avoid `dotnet add package` overrides.
- Find latest: `dotnet list Dependinator.sln package --outdated` or use `./updatepackages -u` (non‑major) / `-m` (allow majors).
- Verify: `dotnet csharpier --version`, `dotnet csharpier --check .`, then `dotnet restore` and `dotnet build` to ensure the MSBuild integration runs cleanly.

## Testing Guidelines
- Frameworks: xUnit is used across test projects; `Moq` and `Verify.Xunit` are also used in parts of the suite (see test `Usings.cs` files).
- Name tests using behavior style, e.g., `MethodName_ShouldDoX()`.
- Place tests alongside feature area (e.g., parsing logic in corresponding `*Tests` project/folder).
- Run all tests with `dotnet test Dependinator.sln`, or target the project you changed.
- Keep tests fast and deterministic; prefer mocks/fakes for external dependencies and file-system heavy operations when possible.
- `Api.Tests` includes:
  - Functions/API auth tests
  - Blob store tests backed by Azurite
- `Dependinator.Core.Tests/Parsing/Solutions/SolutionParserTests.cs` resolves `Dependinator.sln` dynamically; avoid reintroducing hardcoded `/workspaces/...` assumptions in tests.

## Commit & Pull Request Guidelines
- Commits: imperative, concise subject (<72 chars), e.g., `Fix parsing of generic constraints`.
- Group related changes; include rationale in body when useful.
- PRs: clear description, linked issues (`Fixes #123`), screenshots/gifs for UI or diagram changes, and explicit verification steps.
- Ensure build/tests pass locally; include new/updated tests for parsing/core logic changes.

## Security & Configuration Tips
- Do not commit secrets. Use `Api/local.settings.json` locally (example provided).
- Validate third-party updates with `./updatepackages`, then run targeted tests plus a solution build.
- For dependency/package updates, prefer central version changes in `Directory.Packages.props` and avoid per-project version drift.
- Cloud sync uses two auth paths:
  - browser/SWA: SWA session auth via `/.auth/...` and `x-ms-client-principal`
  - VS Code extension host: bearer-token auth via `X-Dependinator-Authorization`
- For deployed SWA/API cloud sync, relevant app settings include:
  - `CloudSync__StorageConnectionString`
  - `CloudSync__OpenIdConfigurationUrl`
  - `CloudSync__BearerAudience`

## Agent-Specific Working Guidance
- Before changing parsing behavior, check both `Dependinator.Core` and `Dependinator.Roslyn` for overlapping logic/tests.
- When editing functionality used by the VS Code extension, verify whether corresponding assets/build steps exist in `DependinatorVsCode/scripts/`.
- When editing cloud sync:
  - browser-hosted `Dependinator.Wasm` uses direct HTTP to `/api`
  - VS Code-hosted `Dependinator.Wasm` routes cloud sync through the extension host
  - the LSP is not the default owner of cloud API communication
- Keep the custom `X-Dependinator-Authorization` header for the VS Code bearer-token path unless there is a clear reason to change it.
- If updating commands/scripts documentation, confirm script contents first (`build`, `run`, `watch`, `updatepackages`, extension scripts).

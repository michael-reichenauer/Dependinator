# Repository Guidelines

## Project Structure & Module Organization
- Solution: `Dependinator.sln` (main .NET projects, currently `net10.0` via `global.json`)
- Runtime/host projects:
  - `DependinatorWeb/` (Blazor Server host for local development)
  - `DependinatorWasm/` (Blazor WebAssembly host / Static Web Apps target)
  - `Api/` (Azure Functions sample host)
  - `DependinatorLanguageServer/` (LSP server executable)
- Core libraries:
  - `Dependinator/` (shared UI + app components)
  - `DependinatorCore/` (core parsing/domain logic/utilities)
  - `DependinatorRoslyn/` (Roslyn-based parsing/integration)
  - `Shared/` (shared DTOs/models)
- Test projects:
  - `Dependinator.Tests/`
  - `DependinatorCore.Tests/`
  - `DependinatorRoslyn.Tests/`
- VS Code extension (not part of `Dependinator.sln`):
  - `DependinatorVsCode/` (TypeScript extension packaging the web UI + language server)
- Common folders you will work in frequently:
  - `Dependinator/App/`, `Dependinator/Diagrams/`
  - `DependinatorCore/Parsing/`, `DependinatorCore/Models/`, `DependinatorCore/Utils/`
  - `DependinatorRoslyn/Parsing/`

## Build, Test, and Development Commands
- Root helper scripts:
  - `./build` (builds `DependinatorWeb`)
  - `./watch` (runs `dotnet watch` for `DependinatorWeb`)
  - `./run` (publishes `DependinatorWasm` in Release and serves `wwwroot` via SWA CLI)
  - `./updatepackages -u` (upgrade non-major packages) or `./updatepackages -m` (allow major)
  - `./build-ext` (npm install + version bump + VS Code extension package build)
- Standard .NET:
  - `dotnet restore`
  - `dotnet build Dependinator.sln`
  - `dotnet test Dependinator.sln`
  - `dotnet run --project DependinatorWeb/DependinatorWeb.csproj`
  - `dotnet run --project DependinatorLanguageServer/DependinatorLanguageServer.csproj`
- Targeted tests:
  - `dotnet test Dependinator.Tests/Dependinator.Tests.csproj`
  - `dotnet test DependinatorCore.Tests/DependinatorCore.Tests.csproj`
  - `dotnet test DependinatorRoslyn.Tests/DependinatorRoslyn.Tests.csproj`
- VS Code extension (`DependinatorVsCode/`):
  - `npm install --prefix ./DependinatorVsCode`
  - `npm run build:extension --prefix ./DependinatorVsCode`
  - `npm run package --prefix ./DependinatorVsCode`
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
- Some helper scripts are convenience wrappers; prefer explicit `dotnet`/`npm` commands when debugging CI or script issues.
- `install-ext` currently references `./build-vscode` (not present in repo); prefer `npm run package/install:vsix --prefix ./DependinatorVsCode` until that wrapper is fixed.

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

## Commit & Pull Request Guidelines
- Commits: imperative, concise subject (<72 chars), e.g., `Fix parsing of generic constraints`.
- Group related changes; include rationale in body when useful.
- PRs: clear description, linked issues (`Fixes #123`), screenshots/gifs for UI or diagram changes, and explicit verification steps.
- Ensure build/tests pass locally; include new/updated tests for parsing/core logic changes.

## Security & Configuration Tips
- Do not commit secrets. Use `Api/local.settings.json` locally (example provided).
- Validate third-party updates with `./updatepackages`, then run targeted tests plus a solution build.
- For dependency/package updates, prefer central version changes in `Directory.Packages.props` and avoid per-project version drift.

## Agent-Specific Working Guidance
- Before changing parsing behavior, check both `DependinatorCore` and `DependinatorRoslyn` for overlapping logic/tests.
- When editing functionality used by the VS Code extension, verify whether corresponding assets/build steps exist in `DependinatorVsCode/scripts/`.
- If updating commands/scripts documentation, confirm script contents first (`build`, `run`, `watch`, `updatepackages`, extension scripts).

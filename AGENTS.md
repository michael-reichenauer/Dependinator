# Repository Guidelines

## Project Structure & Module Organization
- Solution: `Dependinator.sln` with projects:
  - `DependinatorWeb/` (Blazor Server host, main entry)
  - `Dependinator/` (core UI, parsing, models, utils)
  - `DependinatorWasm/` (Blazor WebAssembly sample)
  - `Api/` (Azure Functions sample)
  - `Shared/` (shared DTOs/models)
  - `Dependinator.Tests/` (xUnit tests)
- Common folders in `Dependinator/`: `Parsing/`, `Models/`, `Utils/`, `Diagrams/`, `App/`, `Shared/`.

## Build, Test, and Development Commands
- Build site: `./build` (builds `DependinatorWeb`)
- Run WASM sample: `./run` (runs `DependinatorWasm` in Release)
- Live dev (server): `./watch` (dotnet watch on `DependinatorWeb`)
- Standard .NET:
  - `dotnet build` (solution-wide build)
  - `dotnet test Dependinator.Tests/Dependinator.Tests.csproj`
  - `dotnet run --project DependinatorWeb/DependinatorWeb.csproj`
- Packages and security:
  - `./updatepackages -u` (update non‑major) or `-m` (include major)
  - `dotnet list package --vulnerable` (vulnerability scan)

## Coding Style & Naming Conventions
- Formatting via `.editorconfig` and optional CSharpier (`.csharpierrc.json`).
- Indentation: 4 spaces; UTF‑8 BOM; `this.` not required; braces are not required.
- Prefer `var` over 'explicit' types; System usings first.
- Naming: PascalCase for types/methods/properties and constants.
- Keep files focused; avoid one‑letter identifiers; no inline copyrights.

## CSharpier Formatting
- Tool vs package: `csharpier` (dotnet tool) is separate from `CSharpier.MsBuild` (NuGet that runs during builds). Versions do not need to match.
- Update tool: run `dotnet tool update csharpier` at repo root; this updates `.config/dotnet-tools.json`. To pin, use `--version X.Y.Z`. Teammates run `dotnet tool restore`.
- Update MSBuild package: edit `Directory.Packages.props` to set `<PackageVersion Include="CSharpier.MsBuild" Version="X.Y.Z" />`. Use central management; avoid `dotnet add package` overrides.
- Find latest: `dotnet list Dependinator.sln package --outdated` or use `./updatepackages -u` (non‑major) / `-m` (allow majors).
- Verify: `dotnet csharpier --version`, `dotnet csharpier --check .`, then `dotnet restore` and `dotnet build` to ensure the MSBuild integration runs cleanly.

## Testing Guidelines
- Frameworks: xUnit + Moq (see `Dependinator.Tests/Usings.cs`).
- Name tests using behavior style, e.g., `MethodName_ShouldDoX()`.
- Place unit tests alongside feature area (e.g., parsing → `ParsingTests`).
- Run: `dotnet test`; keep tests fast and deterministic; mock external deps.

## Commit & Pull Request Guidelines
- Commits: imperative, concise subject (<72 chars), e.g., `Fix parsing of generic constraints`.
- Group related changes; include rationale in body when useful.
- PRs: clear description, linked issues (`Fixes #123`), screenshots for UI, and steps to verify.
- Ensure build/tests pass locally; include new/updated tests for logic changes.

## Security & Configuration Tips
- Do not commit secrets. Use `Api/local.settings.json` locally (example provided).
- Validate third‑party updates with `./updatepackages` and run tests after upgrades.

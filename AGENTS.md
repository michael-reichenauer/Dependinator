# Repository Guidelines

## Project Structure & Module Organization
- `DependinatorWeb/` hosts the Blazor Server app entry point; run-time assets live in `wwwroot/`.
- `Dependinator/` contains core UI, parsing, models, utilities, diagrams, and shared app services.
- `Client/` provides the Blazor WebAssembly sample; `Api/` is the Azure Functions sample.
- `Shared/` holds DTOs used across server and client; `Dependinator.Tests/` stores xUnit test suites.
- Solution file `Dependinator.sln` ties projects together; prefer editing via solution-aware tooling.

## Build, Test, and Development Commands
- `./build` builds the Blazor Server host with production settings.
- `./run` publishes and serves the WebAssembly client in Release.
- `./watch` runs `dotnet watch` on `DependinatorWeb` for live reload during development.
- `dotnet build` compiles the full solution; use before PRs to validate project-wide health.
- `dotnet test Dependinator.Tests/Dependinator.Tests.csproj` executes automated tests locally.

## Coding Style & Naming Conventions
- Follow `.editorconfig` rules: 4-space indentation, UTF-8 BOM files, `var` preferred for locals.
- Use PascalCase for types, methods, properties, and constants; avoid one-letter identifiers.
- Formatting is enforced with CSharpier (`dotnet csharpier --check .`); run before committing.

## Testing Guidelines
- Tests rely on xUnit and Moq (see `Dependinator.Tests/Usings.cs`).
- Name tests using behavior phrasing, e.g., `MethodName_ShouldHandleEmptyInput()`.
- Keep tests deterministic and fast; run `dotnet test` prior to pushing changes.

## Commit & Pull Request Guidelines
- Write imperative commit subjects under 72 characters, e.g., `Improve diagram layout caching`.
- Group related changes per commit; include rationale or context in the body when helpful.
- PRs should describe the change, link tracked issues (`Fixes #123`), and add screenshots for UI work.
- Ensure build and tests pass locally; highlight manual verification steps in the PR description.

## Security & Configuration Tips
- Never commit secrets; use `Api/local.settings.json` for local Azure Function settings.
- After package updates (`./updatepackages -u` or `-m`), run `dotnet build` and `dotnet test`.
- Periodically scan dependencies with `dotnet list package --vulnerable` and triage findings quickly.

# Contributing to Dependinator

Thanks for your interest in Dependinator. Bug reports, feature ideas, and pull
requests are all welcome.

## Ways to help

- **Report a bug or request a feature** using the
  [issue templates](https://github.com/michael-reichenauer/Dependinator/issues/new/choose).
- **Try it on your own solutions** and tell us what breaks, what is slow, or what
  is confusing. Real-world feedback is the most valuable input right now.
- **Send a pull request.** Small, focused changes are easiest to review. For
  anything larger, open an issue first so we can agree on the approach.

## Branches and releases

- `dev` is the integration branch: branch from it and target pull requests at it.
- `main` is release-only. Every push to `main` deploys the web app and publishes
  the VS Code extension with a bumped minor version; `./scripts/release` merges
  `dev` into `main` with the changelog bullets.
- Commit messages: imperative, concise subject under 72 characters (for
  example `Fix parsing of generic constraints`), with the rationale in the body
  when it is not obvious.

## Development setup

The easiest way is the devcontainer in `.devcontainer/`: it provisions the .NET
SDKs, Node, Azure Functions Core Tools and the Playwright browsers. Open the
repo in VS Code and choose "Reopen in Container", or use GitHub Codespaces.

### Prerequisites (outside the devcontainer)

- **.NET SDK 11.0 preview** (pinned in `global.json`; the C# 15 union patterns
  need its compiler) plus the **.NET 10 SDK/runtime** for the `net10.0` target.
  Run the `dotnet-install.sh` line from `scripts/installdevtools`.
- **Node.js + npm** for the VS Code extension and the SWA CLI.
- **Azurite** (`npm i -g azurite`), the local Azure Storage emulator, used by
  `./scripts/watch` and `./scripts/run`.
- **Azure Functions Core Tools** (`func`), used by `./scripts/watch` and `./scripts/run`.
- **SWA CLI** (`npm i -g @azure/static-web-apps-cli`), used by `./scripts/run`.
- **Playwright browsers** for `./scripts/e2e`.

Azurite and the SWA CLI are not preinstalled in the devcontainer; add them with
the `npm i -g` commands above when you need `./scripts/watch` or `./scripts/run`.

### Everyday commands

| Task | Command |
| --- | --- |
| Build | `./scripts/build` (or `dotnet build Dependinator.sln`) |
| Run the Blazor Server host with hot reload, plus Azurite and the Functions API | `./scripts/watch` |
| Run the WebAssembly host + API + Azurite locally (as deployed) | `./scripts/run` |
| Unit tests | `dotnet test Dependinator.sln` |
| UI/e2e tests (Playwright, chromium) | `./scripts/e2e` (`-a` for all browsers, `-s` to include cloud-sync tests) |
| Unit + e2e | `./scripts/test` |
| Format check | `dotnet csharpier --check .` |
| Build and install the VS Code extension locally | `./scripts/install-ext` |

The e2e tests target the running app at `http://localhost:5000`; see
`tests/Dependinator.E2E.Tests/README.md`.

## Repository layout

`Dependinator.sln` targets `net10.0`. Production projects live under `src/`,
tests under `tests/`. The dependency direction is `Hosts → UI → Core → Shared`
(with `Roslyn → Core`), enforced by `tests/Dependinator.Architecture.Tests/`.

**Hosts**

- `src/Dependinator.Web/`: Blazor Server host for local development.
- `src/Dependinator.Wasm/`: Blazor WebAssembly host, deployed to Azure Static
  Web Apps as [dependinator.com](https://dependinator.com) and embedded in the
  VS Code extension.
- `src/Dependinator.Lsp/`: Language server executable that parses solutions for the extension.
- `src/Api/`: Azure Functions API for cloud sync.

**Libraries**

- `src/Dependinator.UI/`: shared UI (`App/`, `Diagrams/`, `Modeling/`).
- `src/Dependinator.Core/`: parsing orchestration, domain logic, models, utilities.
- `src/Dependinator.Roslyn/`: Roslyn-based parsing.
- `src/Shared/`: DTOs shared between client and API.

**Tests**

- `tests/*.Tests/`: xUnit unit tests per library and host.
- `tests/Dependinator.Architecture.Tests/`: NetArchTest layering guards.
- `tests/Dependinator.E2E.Tests/`: Playwright UI tests.

**VS Code extension** (not part of the solution)

- `src/DependinatorVsCode/`: TypeScript extension packaging the web UI and the
  language server. See its [DEVELOPMENT.md](src/DependinatorVsCode/DEVELOPMENT.md).

## Conventions

- **Formatting:** CSharpier (`.csharpierrc.json`) and `.editorconfig`. The build
  enforces formatting, so run `dotnet csharpier format .` before committing.
- **Style:** 4-space indent, explicit types over `var`, PascalCase for
  types/methods/properties/constants, nullable-aware code, braces preferred.
- **Errors:** fallible operations return `Result` / `Result<T>`
  (`src/Dependinator.Core/Utils/Result.cs`) instead of throwing.
- **Tests:** xUnit + Moq + Verify.Xunit. Name tests `MethodName_ShouldDoX()`.
  UI changes should get an e2e test.
- **Package versions** are managed centrally in `Directory.Packages.props`.

## Cloud sync and deployment

Cloud sync is optional for users and uses [Clerk](https://clerk.com) for
authentication (magic links / email OTP). The API validates Clerk-issued JWTs
via JWKS.

- The web app is deployed to Azure Static Web Apps by the CI/CD workflow;
  `swa-cli.config.json` holds the local SWA CLI configuration. The Static Web App
  needs these application settings: `CloudSync__ClerkIssuer`,
  `CloudSync__ContainerName`, `CloudSync__MaxUserQuotaBytes`,
  `CloudSync__StorageConnectionString`.
- The VS Code extension serves a local Clerk sign-in page and stores the session
  JWT in VS Code secrets. The `dependinator.cloudSync.baseUrl` setting controls
  which API endpoint it uses.

## License

By contributing you agree that your contributions are licensed under the
[MIT License](LICENSE).

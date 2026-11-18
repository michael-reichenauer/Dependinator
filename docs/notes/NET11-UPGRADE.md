# .NET 11 / C# 15 follow-up checklist

The `Result` / `Result<T>` union types (`src/Dependinator.Core/Utils/Result.cs`) need the C# 15 compiler,
so the repo builds with the **.NET 11 SDK** (`global.json`). Phase A, the move from the RC1 SDK to the
GA SDK, is done. Phase B1, the move of every project but the Api to **net11.0**, is done. Phase B2, the
move of `src/Api`, `src/Shared` and `tests/Api.Tests`, waits for Azure Static Web Apps.
Background: the `result-union-kit.md` notes (kept outside the repo), section 7.

## Phase A: .NET 11 GA SDK, target framework stays net10.0 (done Nov 10, 2026)

- `global.json` pins `11.0.100` with `"rollForward": "latestFeature"`, no `allowPrerelease`.
- `Directory.Build.props` sets `<LangVersion>15</LangVersion>` (net10.0 defaults to C# 14) and no longer
  suppresses the preview SDK notice. `CS8509` stays in `WarningsAsErrors`.
- `.devcontainer/Dockerfile` and `scripts/installdevtools` install `--channel 11.0` without
  `--quality preview`. An existing devcontainer needs a rebuild, or that line run once.
  `scripts/cloud-setup` installs the exact `global.json` version, so it followed by itself.
- CI: no change. The workflows keep `dotnet-version: "10.0.x"` (the net10.0 runtime the tests, the
  csharpier tool and CSharpier.MsBuild need) next to `global-json-file: global.json`.
- `Microsoft.CodeAnalysis.*` is at 5.9.0, the newest release at GA. `Microsoft.Build`, `.Framework` and
  `Microsoft.NET.StringTools` stay at 18.9.6: from 18.10 they ship `lib/net11.0` only, and every net10.0
  build then warns "doesn't support net10.0". `.github/dependabot.yml` ignores 18.10+ for the three;
  `./scripts/updatepackages -u` does not, so revert them if it bumps them. They move in phase B.
- No **CS0104** (ambiguous `Result`, `Error`, `Success`) showed up with the GA base class library. If a
  later SDK brings one: resolve it with `using Result = Dependinator.Core.Utils.Result;` style aliases
  in the affected `Usings.cs`, or rename ours; do not remove the global using of `Dependinator.Core.Utils`.
- Still open, optional: CSharpier 1.3.0 is the version in use; check its release notes for C# 15
  support. Until it parses the `union` keyword, keep the hand-written union structs. Converting
  `Result.cs` and `src/Dependinator.UI/Shared/CloudSync/SyncDownOutcome.cs` to `union` declarations
  later is optional; note a compiler-generated union adds the `HasValue`/`TryGetValue` members the kit
  deliberately leaves out, and the `Error` accessor and `Result<T>` → `Result` conversion would have
  to be kept as extensions.

Verify (all must be clean, as for every migration commit):

```bash
dotnet --version                                   # 11.0.1xx, no "rc"
dotnet build Dependinator.slnx 2>&1 | grep -E "warning|error"     # nothing
dotnet build src/Dependinator.DemoGen/Dependinator.DemoGen.csproj
dotnet csharpier check .
dotnet test Dependinator.slnx                      # read every "Passed!" line, never only the tail
./scripts/e2e -s -a                                # needs func + azurite (npm i -g azurite)
dotnet publish src/Dependinator.Wasm/Dependinator.Wasm.csproj -c Release -o /tmp/wasm
dotnet publish src/Dependinator.Lsp/Dependinator.Lsp.csproj -c Release -r linux-x64 --self-contained true -o /tmp/lsp
dotnet publish src/Api/Api.csproj -c Release -o /tmp/api
./scripts/gen-demo                                 # runs to completion; logs "Using MSBuild 11.x" since phase B1
```

## Phase B1: every project but the Api to net11.0 (done Nov 18, 2026)

Static Web Apps only uploads the prebuilt Wasm app (`skip_app_build: true` in the deploy steps), so
only its managed functions, the Api, are limited to the runtimes it lists. `src/Shared` stays with the
Api; a net11.0 project references the net10.0 Shared without trouble, and Shared has no unions.

- All `*.csproj` but `src/Api`, `src/Shared` and `tests/Api.Tests` target `net11.0`, `src/Dependinator.DemoGen`
  included.
- `UnionPolyfill.cs` is deleted: only Core declared it (internal, under `#if !NET11_0_OR_GREATER`), and
  Core and UI now get `UnionAttribute`/`IUnion` from the base class library. `<LangVersion>15</LangVersion>`
  is gone from `Directory.Build.props` (C# 15 is the net11.0 default; the Api and Shared use no C# 15).
- Packages: `Microsoft.AspNetCore.Components.Web`, `.WebAssembly`, `.WebAssembly.DevServer` 10.0.15 →
  11.0.0 (MudBlazor 9.14's net11.0 target requires 11.0.0, else NU1605); `Microsoft.Build`,
  `.Framework`, `Microsoft.NET.StringTools` 18.9.6 → 18.12.2 (the MSBuild of the 11.0.100 SDK), and
  their Dependabot `ignore` entries are deleted. Only net11.0 projects reference them.
- Blazor 11's analyzers flagged three spots: `AskPanel.razor` (BL0012, `StateHasChanged` after an
  `EventCallback`) and `JSInterop.cs` (BL0016, the module import, deliberately not caught).
- `MSBuildLocator` now offers the net11.0 processes the 11.x SDK, so the Roslyn parser, its tests and
  `./scripts/gen-demo` load MSBuild 11.x.
- Paths: `languageServer.ts` (dev fallback), `.vscode/launch.json` (Web host), `e2e.yml` (Playwright).
- CI keeps `dotnet-version: "10.0.x"`, now for the Api, Api.Tests and CSharpier. The devcontainer and
  `scripts/installdevtools` keep installing both SDKs.

## Phase B2: the Api and Shared to net11.0

net10.0 is LTS; net11.0 is STS. Move when Azure Static Web Apps supports .NET 11 functions.

Status on Nov 10, 2026: Azure Functions announced .NET 11 as generally available for the isolated
worker, and it requires the new `Azure.Functions.Sdk` project SDK (1.0.1 or later) in `src/Api/Api.csproj`.
Azure Static Web Apps, which hosts the Api as managed functions, lists `dotnet-isolated:10.0` as its
newest `apiRuntime`, no 11.0. So the gate is not met: check the Static Web Apps "Supported languages
and runtimes" page again before starting.

- [ ] `src/Api/Api.csproj`, `src/Shared/Shared.csproj`, `tests/Api.Tests/Api.Tests.csproj`:
      `<TargetFramework>net10.0</TargetFramework>` → `net11.0`.
- [ ] `src/Api/Api.csproj`: move to the `Azure.Functions.Sdk` project SDK that .NET 11 function apps
      require; `Microsoft.Azure.Functions.Worker*` to versions that support net11.0.
- [ ] **`src/Dependinator.Wasm/staticwebapp.config.json`: fix `platform.apiRuntime`.** It still says
      `dotnet-isolated:8.0` although the Api has targeted net10.0 for a while (found Nov 18, 2026, left
      as it is then since cloud sync works). Set it to the 11.0 value (`dotnet-isolated:11.0`), and check
      on the PR's test site that cloud sync still works.
- [ ] Workflows: remove the `dotnet-version: "10.0.x"` line in `.github/workflows/unit-tests.yml`,
      `e2e.yml`, `release.yml` and `azure-static-web-apps-polite-island-0e6a6eb03.yml` (the
      11 SDK carries the 11 runtime), unless CSharpier still needs it (below).
- [ ] `.devcontainer/Dockerfile`: `FROM mcr.microsoft.com/dotnet/sdk:11.0`, delete the
      `dotnet-install.sh` RUN block and its comment. `scripts/installdevtools`: delete the install line
      and its echo. `scripts/cloud-setup`: drop SDK 10. `.config/dotnet-tools.json`: the csharpier tool
      has `"rollForward": false`, so its version must support the net11 runtime (or set `rollForward`
      to `true`).
- [ ] Hardcoded `net10.0` paths: `.vscode/tasks.json` (Api `cwd`), `.vscode/settings.json`
      (`azureFunctions.deploySubpath`).
- [ ] Docs: `README.md`, `CLAUDE.md` line 11, `CONTRIBUTING.md`, `src/Dependinator.VsCodeExt/DEVELOPMENT.md`,
      `docs/ARCHITECTURE.md` (Stack row, "Two target frameworks" risk).
- [ ] Run the Phase A verification list again, including the Api publish and `./scripts/e2e -s`
      (cloud sync).

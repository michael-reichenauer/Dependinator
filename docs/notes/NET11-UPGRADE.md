# .NET 11 / C# 15 follow-up checklist

The `Result` / `Result<T>` union types (`src/Dependinator.Core/Utils/Result.cs`) need the C# 15 compiler,
so the repo builds with the **.NET 11 SDK** (`global.json`) while every project still targets
**net10.0**. Phase A, the move from the RC1 SDK to the GA SDK, is done. Phase B, the move of the target
framework, is optional and waits for Azure Static Web Apps.
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
dotnet build Dependinator.sln 2>&1 | grep -E "warning|error"      # nothing
dotnet build src/Dependinator.DemoGen/Dependinator.DemoGen.csproj
dotnet csharpier check .
dotnet test Dependinator.sln                       # read every "Passed!" line, never only the tail
./scripts/e2e -s -a                                # needs func + azurite (npm i -g azurite)
dotnet publish src/Dependinator.Wasm/Dependinator.Wasm.csproj -c Release -o /tmp/wasm
dotnet publish src/Dependinator.Lsp/Dependinator.Lsp.csproj -c Release -r linux-x64 --self-contained true -o /tmp/lsp
dotnet publish src/Api/Api.csproj -c Release -o /tmp/api
./scripts/gen-demo                                 # runs to completion; "Using MSBuild 10.x" stays until phase B
```

## Phase B (optional): move the target framework to net11.0

net10.0 is LTS (supported to November 2028); net11.0 is STS. Move only when every host supports it.
Gate: the **Azure Functions isolated worker** (`src/Api`) and **Azure Static Web Apps** must support
.NET 11; the Api project can stay on net10.0 alone if needed (it does not reference `Dependinator.Core`).

Status on Nov 10, 2026: Azure Functions announced .NET 11 as generally available for the isolated
worker, and it requires the new `Azure.Functions.Sdk` project SDK (1.0.1 or later) in `src/Api/Api.csproj`.
Azure Static Web Apps, which hosts the Api as managed functions, lists `dotnet-isolated:10.0` as its
newest `apiRuntime`, no 11.0. So the gate is not met: check the Static Web Apps "Supported languages
and runtimes" page again before starting.

- [ ] Every `*.csproj` under `src/` and `tests/` (18 projects, including `src/Dependinator.DemoGen`
      which is outside `Dependinator.sln`): `<TargetFramework>net10.0</TargetFramework>` → `net11.0`.
- [ ] Delete `src/Dependinator.Core/Utils/UnionPolyfill.cs`: the base class library ships
      `UnionAttribute` and `IUnion` (the file is `#if !NET11_0_OR_GREATER`, so it is already empty on
      net11.0). Keep the `[Union]`/`IUnion` uses in `Result.cs` and `SyncDownOutcome.cs` as they are.
- [ ] `Directory.Build.props`: remove `<LangVersion>` (C# 15 is the default). Keep `WarningsAsErrors`
      with `CS8509`.
- [ ] Workflows: remove the `dotnet-version: "10.0.x"` line in `.github/workflows/unit-tests.yml`,
      `e2e.yml`, `release.yml` and `azure-static-web-apps-polite-island-0e6a6eb03.yml` (the
      11 SDK carries the 11 runtime). In `e2e.yml` change the Playwright path
      `tests/Dependinator.E2E.Tests/bin/Debug/net10.0/playwright.ps1` to `net11.0`.
- [ ] `.devcontainer/Dockerfile`: `FROM mcr.microsoft.com/dotnet/sdk:11.0`, delete the
      `dotnet-install.sh` RUN block and its comment. `scripts/installdevtools`: delete the install line
      and its echo. `.config/dotnet-tools.json`: the csharpier tool has `"rollForward": false`, so its
      version must support the net11 runtime (or set `rollForward` to `true`).
- [ ] Hardcoded `net10.0` paths: `src/DependinatorVsCode/src/languageServer.ts` (two entries, the dev
      fallback to `bin/{Debug,Release}/net10.0/Dependinator.Lsp.dll`), `.vscode/launch.json`
      (`Dependinator.Web.dll`), `.vscode/tasks.json` (Api `cwd`), `.vscode/settings.json`
      (`azureFunctions.deploySubpath`).
- [ ] `Directory.Packages.props`: `Microsoft.AspNetCore.Components.WebAssembly`,
      `Microsoft.AspNetCore.Components.WebAssembly.DevServer`, `Microsoft.AspNetCore.Components.Web`
      10.0.x → 11.0.x; `Microsoft.Azure.Functions.Worker*` to versions that support net11.0;
      `./scripts/updatepackages -m` for the rest.
- [ ] `Directory.Packages.props`: `Microsoft.Build`, `.Framework`, `Microsoft.NET.StringTools` 18.9.6 →
      the 18.10+ line that ships with the 11 SDK, and delete their `ignore` entries in
      `.github/dependabot.yml`.
- [ ] `dotnet workload restore Dependinator.sln` on the new band (a no-op today because
      `WasmBuildNative` is false; keep the CI step either way).
- [ ] `src/Api/Api.csproj`: move to the `Azure.Functions.Sdk` project SDK that .NET 11 function apps
      require, and set `apiRuntime` in `src/Dependinator.Wasm/staticwebapp.config.json` to the 11.0 value.
- [ ] Docs: `README.md` (Solution structure and Prerequisites), `CLAUDE.md` line 11,
      `src/DependinatorVsCode/DEVELOPMENT.md`; the "net10.0" mentions in `Directory.Build.props` and
      `UnionPolyfill.cs` comments disappear with those edits.
- [ ] `src/Dependinator.Core/Parsing/Utils/MSBuildLocatorHelper.cs`: no code change, but note that a
      net11.0 process is offered the 11.x SDK by `MSBuildLocator` (today it is offered only 10.x), so
      `./scripts/gen-demo` and the Roslyn tests then log "Using MSBuild 11.x".
- [ ] Run the Phase A verification list again, plus `./scripts/build-ext` and an install of the VSIX
      (`./scripts/install-ext`), since the extension publishes the LSP self-contained per RID.

# .NET 11 / C# 15 follow-up checklist

The `Result` / `Result<T>` union types (`src/Dependinator.Core/Utils/Result.cs`) need the C# 15 compiler,
so the repo builds with the **.NET 11 RC1 SDK** (`global.json`) while every project still targets
**net10.0**. Two follow-ups remain, in order. The first is small and due at GA; the second is optional.
Background: `result-union-kit.md`, section 7.

## Phase A: .NET 11 GA (expected November 2026), target framework stays net10.0

Required as soon as the GA SDK is out; nothing else in the repo changes.

- [ ] `global.json`: `"version": "11.0.100"` (the GA version), remove `"allowPrerelease": true`, keep
      `"rollForward": "latestFeature"`.
- [ ] `Directory.Build.props`: `<LangVersion>preview</LangVersion>` → `<LangVersion>15</LangVersion>`.
      Keep `CS8509` in `WarningsAsErrors`.
- [ ] `.devcontainer/Dockerfile` (the `dotnet-install.sh` RUN line) and `scripts/installdevtools`
      (the same line): drop `--quality preview`; `--channel 11.0` then resolves to GA. Rebuild the
      devcontainer, or run the line once in an existing one.
- [ ] `README.md`, Prerequisites: ".NET SDK 11.0 preview" → ".NET SDK 11.0". `CLAUDE.md` line 11:
      "the .NET 11 preview SDK" → "the .NET 11 SDK". `src/DependinatorVsCode/DEVELOPMENT.md`:
      "the .NET 11 preview SDK" → "the .NET 11 SDK".
- [ ] CI: **no change**. The four workflows keep `dotnet-version: "10.0.x"` (the net10.0 runtime the
      tests, the csharpier tool and CSharpier.MsBuild need) next to `global-json-file: global.json`.
- [ ] `Directory.Packages.props`: move `Microsoft.CodeAnalysis.CSharp`, `.Workspaces.MSBuild`,
      `.CSharp.Workspaces` (5.6.0) and `Microsoft.Build`, `.Framework`, `Microsoft.NET.StringTools`
      (18.9.6) to the versions that ship with the GA SDK, so Dependinator parses user code that uses
      C# 15 syntax (the union *patterns* already bind with 5.6.0; the `union` *keyword* may not).
      `./scripts/updatepackages -u` then `dotnet test tests/Dependinator.Roslyn.Tests`.
- [ ] If the build reports **CS0104** (ambiguous `Result`, `Error`, `Success`): the base class library
      shipped a type of that name. Resolve with `using Result = Dependinator.Core.Utils.Result;` style
      aliases in the affected `Usings.cs`, or rename ours; do not remove the global using of
      `Dependinator.Core.Utils`.
- [ ] CSharpier: check the release notes for C# 15 support. Until it parses the `union` keyword, keep
      the hand-written union structs. Converting `Result.cs` and
      `src/Dependinator.UI/Shared/CloudSync/SyncDownOutcome.cs` to `union` declarations later is
      optional; note a compiler-generated union adds the `HasValue`/`TryGetValue` members the kit
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
.NET 11, which usually lags GA by months; the Api project can stay on net10.0 alone if needed (it does
not reference `Dependinator.Core`).

- [ ] Every `*.csproj` under `src/` and `tests/` (18 projects, including `src/Dependinator.DemoGen`
      which is outside `Dependinator.sln`): `<TargetFramework>net10.0</TargetFramework>` → `net11.0`.
- [ ] Delete `src/Dependinator.Core/Utils/UnionPolyfill.cs`: the base class library ships
      `UnionAttribute` and `IUnion` (the file is `#if !NET11_0_OR_GREATER`, so it is already empty on
      net11.0). Keep the `[Union]`/`IUnion` uses in `Result.cs` and `SyncDownOutcome.cs` as they are.
- [ ] `Directory.Build.props`: remove `<LangVersion>` (C# 15 is the default). Keep `WarningsAsErrors`
      with `CS8509`.
- [ ] Workflows: remove the `dotnet-version: "10.0.x"` line in `.github/workflows/unit-tests.yml`,
      `e2e.yml`, `vscode-extension.yml` and `azure-static-web-apps-polite-island-0e6a6eb03.yml` (the
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
- [ ] `dotnet workload restore Dependinator.sln` on the new band (a no-op today because
      `WasmBuildNative` is false; keep the CI step either way).
- [ ] Docs: `README.md` (Solution structure and Prerequisites), `CLAUDE.md` line 11,
      `src/DependinatorVsCode/DEVELOPMENT.md`; the "net10.0" mentions in `Directory.Build.props` and
      `UnionPolyfill.cs` comments disappear with those edits.
- [ ] `src/Dependinator.Core/Parsing/Utils/MSBuildLocatorHelper.cs`: no code change, but note that a
      net11.0 process is offered the 11.x SDK by `MSBuildLocator` (today it is offered only 10.x), so
      `./scripts/gen-demo` and the Roslyn tests then log "Using MSBuild 11.x".
- [ ] Run the Phase A verification list again, plus `./scripts/build-ext` and an install of the VSIX
      (`./scripts/install-ext`), since the extension publishes the LSP self-contained per RID.

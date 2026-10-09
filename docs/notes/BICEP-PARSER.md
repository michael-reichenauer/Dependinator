# Bicep parser: design and parked follow-ups

Added October 2026 as the first non-C# parser (`src/Dependinator.Bicep/`). This note records the
decisions behind it and what was deliberately left for later.

## Engine

The official compiler library `Azure.Bicep.Core` (NuGet, net10.0, MIT), the same code the Bicep
CLI and VS Code extension run. Microsoft calls the package unsupported and may change its API in any
release, so the version is pinned in `Directory.Packages.props` and ignored by Dependabot: bump it by
hand and build. `BicepCompiler.Create()` builds the compiler's own service container, kept private
to the parser (`BicepCompilerHost`), and the one compiler per process is reused by every parse.
Compilation runs without restore, so registry modules (`br:`, `ts:`) are never downloaded; the
parse needs no network and no credentials.

Cost: the self-contained LSP publish grows from 115 MB to 180 MB on linux-x64 (measured 2026-10-09,
`dotnet publish -c Release -r linux-x64 --self-contained`); 37 MB of that is `Azure.Bicep.Types.Az.dll`,
the Azure resource type index, the rest the compiler, the Azure SDK and registry client libraries.

## What is a model

Each **entry point** `.bicep` file in the workspace is one model: a `.bicep` file no other `.bicep`
file in the workspace uses as a module or imports (`BicepEntryPoints`, a cheap text scan done by
`WorkspaceFileService` next to the `.sln` discovery). The model path is that file; the modules it
references are parsed as part of it.

## Model shape

| Bicep | Node | Name |
|---|---|---|
| entry file | `Solution`, icon of the target scope (resource group, subscription, management group) | `main*bicep` (shown as `main.bicep`) |
| param / var / output | `ParameterMember` / `VariableMember` (private) / `OutputMember` rows | `main*bicep.location` |
| resource | `Resource`, Azure icon of its type (`AzureIcons`) | `main*bicep.web` |
| child resource (nested or `parent:`) | nested under its parent | `main*bicep.web.config` |
| module | `Module`; a local module's file is expanded beneath it, recursively | `main*bicep.storage`, `main*bicep.storage.account` |
| `existing` resource | under `$Externals` (deployed elsewhere, only referenced) | `$Externals.vnet` |

An output may share its identifier with a parameter or variable (outputs are not referenceable, the
AVM `output name` idiom); such an output is named `accountName (output)`.

Links come from the compiler's dependency graph (`ResourceDependencyVisitor`: `dependsOn`,
`parent`, `scope`, property reads, through variables) plus the symbols each declaration references
(walked without descending into nested child resources). A module's `params` link the module's own
parameter nodes to the given values and `module.outputs.x` reads link to the output node, so the
diagram shows the value flow instead of a coarse link to the whole module.

Icons: `AzureIcons` maps resource types to the Azure icons in the UI library; a child type falls back
to its parents, then a provider default, then the generic tintable `Cloud` icon. A parsed icon is a
new generic facility (`NodeProperties.IconName` / `Node.IconName`); a user's custom icon still wins
and survives re-parse.

## Example deployment

`examples/bicep/webshop.bicep` (subscription scope, 21 files, ~2,900 lines) is a realistic
fictional e-commerce platform: resource groups, network, identity, monitoring, Key Vault, SQL,
storage, Cosmos DB, Redis, Service Bus, Event Grid, App Service, Functions, Container Apps, API
Management, Front Door, DNS records in an existing zone, and optional AI Search and OpenAI. It
exercises loops, conditionals, nested children, `parent:` and `scope:`, an existing parent with
deployed children, module outputs flowing into other modules' parameters, user-defined types and
a shared module expanded ten times. `WebshopExampleTests` compiles it without errors and checks
the model shape, so it doubles as a regression guard for the pinned compiler package.

## Demo model

`/Demo.bicep` is the example deployment pre-parsed by `./scripts/gen-demo bicep` into
`src/Dependinator.Wasm/wwwroot/demo-bicep.model` (root renamed `webshop*bicep` -> `Demo*bicep`),
served to the browser app as a static asset and embedded into `Dependinator.Bicep`, so the Web/LSP
hosts open it without the compiler (`BicepParser` handles the demo path first). `DemoModel.ListedPaths`
offers both demos in the Models menu in the standalone web app and the e2e test host ("Demo models"
section in Manage Models); share links use the key `demo-bicep`. Regenerate it after changing the
example; `BicepParserTests` and the e2e `BicepDemoTests` check a few of its facts.

## Parked follow-ups

- ARM JSON templates as entry points or expanded modules (today a JSON module is a leaf).
- `.bicepparam` files (a `using` does not make the target file a module; parameter files are not
  models).
- Registry module restore (`CreateCompilation(skipRestore: false)` needs network and credentials),
  which would expand `br:`/`ts:` modules.
- Indexed module collections (`mods[i].outputs.x`), currently linked to the module node.
- Dedicated glyphs for parameters, variables and outputs (placeholders: Property, Field, Event) and
  a legend row for infrastructure nodes.
- Choosing among several entry points when a workspace has many (all are listed; the first
  solution, then the first deployment, is the startup model).
- Resource kinds that the type alone cannot tell apart (a function app is `Microsoft.Web/sites`,
  OpenAI is `Microsoft.CognitiveServices/accounts`) keep the type's icon.
- Terraform, TypeScript/JavaScript and Go parsers, and `.slnx`/`.csproj` entry points, follow the
  same seam: an `ISourceParser` with `CanParse`, registered by the hosts.

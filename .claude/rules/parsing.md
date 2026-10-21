---
paths:
  - "src/Dependinator.Core/Parsing/**"
  - "src/Dependinator.Roslyn/**"
  - "src/Dependinator.Bicep/**"
  - "src/Dependinator.Lsp/**"
  - "src/Dependinator.DemoGen/**"
---

# Parsing: traps and decisions

- **Parse settings:** `IParserService` is `[Rpc]`. In VS Code the parse runs in the LSP process,
  so parse settings must travel as method arguments on `SolutionParseOptions`
  (`Core/Parsing/Types.cs`). A static, singleton or config read on the parser side does nothing
  there, while it still works in Blazor Server. Read the settings through `modelMgr.WithModel(...)`
  before the parse await (the model lock is a thread-affine `Monitor`).
- **Naming:** never name a Core.Parsing type `ParseOptions`: it clashes (CS0104) with
  `Microsoft.CodeAnalysis.ParseOptions` in Roslyn's `SourceParser.cs`. Prefix new type names.
- **Adding a parser** takes:
  - a library `X → Core` with a `[Transient] class : ISourceParser` that implements `CanParse`;
  - `AddDependinatorXServices()`, registered in the Lsp and Web `Program.cs`;
  - an architecture-test rule;
  - `ModelPaths` / `WorkspaceFileService` discovery for its file kind.
- **Bicep** (beyond CLAUDE.md; design and follow-ups in `docs/notes/BICEP-PARSER.md`):
  - Never import `Bicep.Core.Parsing` or `Bicep.Core.Utils`: they are twins of Dependinator names.
  - JsonPath.Net's root namespace `Json` shadows Core's `Json` helper in every project that
    references Dependinator.Bicep (DemoGen aliases it `ModelJson`).
  - Compilation runs without restore (`CreateCompilationWithoutRestore`), so registry modules
    stay leaf nodes. There is one `BicepCompiler` per process (`BicepCompilerHost`).
- **C# member names** (decided Oct 2026):
  - Extension members keep `Ext.extension(global::Ns.T).Member`. `NodeName` treats `()`, `[]`
    and `<>` as bracket groups whose dots don't split, and a bracket counts only once it closes,
    so "Latency < 50ms" and `operator<` stay plain text.
  - Conversion operators are named `implicitoperatorResult(…)` and static constructors
    `staticType()`; constructors are `ConstructorMember`.
  - Links target `OriginalDefinition` / `ReducedFrom`, not the constructed use site.
- **A changed name-derived parent:** `StructureService` moves the existing node. There is
  deliberately no `ModelDto` format bump, since it would drop layout, notes and accepted cycles.
  Models that were never re-parsed (standalone Wasm, cloud-synced) keep the old shapes, so
  `CyclePrompt` keeps its workarounds.
- **Namespace comments:** a comment directly above `namespace X;` (not a header followed by a
  blank line) becomes the namespace's description. Comments from several files are all shown,
  each after its location (`Util.cs:3: …`), and identical ones once. `NamespaceDoc.cs` is the
  recommended home, but it is not enforced.
- **Still open:**
  - links to delegate `Invoke` get made-up targets;
  - type arguments of used generics get no link (`Result<ModelDto>` gives no ModelDto link);
  - demo generation logs compile errors in Api and UI, likely because DemoGen's MSBuildLocator
    picks SDK 10, which fails on the C# 15 unions.
- **Demo models:** `./scripts/gen-demo csharp` changes facts that the e2e tests rely on, so run the
  full e2e suite afterwards; `./scripts/gen-demo bicep` is safe. `./scripts/record-demo` once left
  `demo.model` modified (cause unknown): check `git status` after recording.

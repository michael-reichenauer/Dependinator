---
paths:
  - "src/Dependinator.Core/Parsing/**"
  - "src/Dependinator.Roslyn/**"
  - "src/Dependinator.Bicep/**"
  - "src/Dependinator.TypeScript/**"
  - "src/Dependinator.Core/Shared/ScriptProjects.cs"
  - "src/Dependinator.Lsp/**"
  - "src/Dependinator.DemoGen/**"
  - "src/Dependinator.Core/Shared/ModelPaths.cs"
  - "src/Dependinator.Core/Shared/WorkspaceFileService.cs"
  - "src/Dependinator.UI/Modeling/ModelService.cs"
  - "src/Dependinator.UI/Modeling/StructureService.cs"
  - "src/Dependinator.UI/Diagrams/Cycles/CyclePrompt*.cs"
  - "src/Dependinator.Web/Program.cs"
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
  - `ModelPaths` / `WorkspaceFileService` discovery for its file kind;
  - for a language with its own toolchain, a helper program run with `HelperProcess` (see
    TypeScript below) rather than a port of its compiler.
- **Source generators:** `MSBuildWorkspace` runs the generators Roslyn's loader can load (the
  Functions SDK, Regex); `Compiler.RunUnloadableSourceGenerators` runs only the rest (Razor).
  Running a loadable one again defines its types twice: that gave 49 false compile errors in Api
  and 12 in UI until Oct 21, found once the problem indicator showed them.
- **Razor locations:** a component's types live in generated trees whose paths are not on disk.
  Take spans with `Locations.GetSourceSpan` (never `GetLineSpan()`): it follows `#line` to the
  `.razor` line for `@code` members, and maps the class itself to the whole `.razor` file named by
  the generated file's `#pragma checksum`. Without it Show Source Code did nothing (Oct 29).
- **Language version:** the parser compiles with its own Roslyn package, which can be older than
  the user's SDK. A `LangVersion` it does not know ("15" with Roslyn 5.9) silently became its
  default, and every union was a false "feature is in Preview" compile error that no fix cleared
  (Nov 11, after the move to `LangVersion` 15). `Compiler.WithUsableLanguageVersion` compiles such
  projects with `LanguageVersion.Preview`; `CompilerTests` guards it on Dependinator.Mcp.
- **Parse problems:** a parser reports what can leave the model incomplete as a `ParseProblem`
  item (`Types.cs`), one brief summary per project or file, never per error. DemoGen leaves them
  out of the demo models.
- **Bicep** (beyond CLAUDE.md; design and follow-ups in `docs/notes/BICEP-PARSER.md`):
  - Never import `Bicep.Core.Parsing` or `Bicep.Core.Utils`: they are twins of Dependinator names.
  - JsonPath.Net's root namespace `Json` shadows Core's `Json` helper in every project that
    references Dependinator.Bicep (DemoGen aliases it `ModelJson`).
  - Compilation runs without restore (`CreateCompilationWithoutRestore`), so registry modules
    stay leaf nodes. There is one `BicepCompiler` per process (`BicepCompilerHost`).
- **TypeScript and JavaScript** (beyond CLAUDE.md; design and follow-ups in
  `docs/notes/TYPESCRIPT-PARSER.md`):
  - The parser is a Node.js program (`src/Dependinator.TypeScript/Helper/`, the TypeScript 6
    compiler API) that prints `Parsing.Item` JSON; the C# side only runs it. Its item types
    (`Helper/src/items.ts`) mirror `Types.cs` by name: a new `NodeType` or property goes in both.
  - The helper is bundled into one script and embedded in the dll by the csproj's `BuildHelper`
    target (`npm ci`, esbuild), so a solution build needs Node.js and npm. After changing
    `Helper/src`, a normal `dotnet build` rebuilds it; `cd Helper && npm run check` type-checks.
    To try the helper alone: `node Helper/dist/ts-parser.js <tsconfig.json>` (items on stdout).
  - A bundled compiler has no `lib.*.d.ts` beside it: without them every `Promise`, array and
    DOM type is unknown and signatures come out empty, with no error. They are bundled as a
    virtual module (`build.mjs`, `libraries.ts`).
  - Symbols: resolve an import's alias (`getAliasedSymbol`) before looking at its declaration; a
    CommonJS file's module symbol is only on the file (`file.symbol`, internal); a symbol whose
    declaration is a source file or an ambient module is "the whole module" and makes no link.
  - Node names: a literal dot in a file, folder or package name is `*` (`names.ts`), as for C#
    project files. Lines are 0-based.
  - In VS Code the parser runs on the editor's own runtime (`initializationOptions.nodePath`,
    `ELECTRON_RUN_AS_NODE=1`). This cannot be tried in a cloud session.
  - The extension tells the UI the kinds of the changed files ("script", "code"); a new source
    kind that is watched needs its kind in `IModelService.IsParsedFrom`.
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

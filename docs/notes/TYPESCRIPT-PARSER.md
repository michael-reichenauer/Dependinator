# TypeScript and JavaScript parser: design and parked follow-ups

Added November 2026 as the first parser that is not written in C# (`src/Dependinator.TypeScript/`).
This note records the decisions behind it and what was deliberately left for later. It is also the
template for the next language (Go): a small helper program in the language's own toolchain that
prints the model as items.

## Engine: a helper process

The TypeScript compiler is the only tool that resolves TypeScript and JavaScript the way the
editor does (path aliases, `node_modules`, declaration files, CommonJS, JSDoc types), and it is a
Node.js library. So the parser is a small Node.js program, `Helper/` (`ts-parser.js`), that the C#
side starts once per parse:

```
node ts-parser.js <tsconfig.json | jsconfig.json | package.json> [--include-tests]
```

It prints the model to stdout as the JSON of Core's `Parsing.Item` list (the property names and
enum names of `Types.cs`, see `Helper/src/items.ts`) and progress and errors to stderr; a non-zero
exit code is a failed parse, with the last stderr line as the error. `TypeScriptParser` (C#) only
starts it (`HelperProcess.RunAsync` in Core, a 5 minute limit) and deserializes the items.

Rejected: a TypeScript parser in C# (none resolves symbols), tree-sitter (syntax only: no resolved
imports, no types, so links would be guesses), and the native TypeScript 7 compiler (its tool API
is not stable before 7.1). TypeScript is pinned to 6.0.3, the last JavaScript-hosted line.

**The script** is one file: esbuild bundles the helper and the compiler (`Helper/build.mjs`),
with the compiler's `lib.*.d.ts` files inside it (a virtual module served by a custom compiler
host, `libraries.ts`), because types like `Promise` and `Array` come from them and a bundled
compiler has no `lib` folder beside it. About 7 MB. The C# project builds it before compiling (the
`BuildHelper` target: `npm ci` and `npm run build`, skipped while the output is newer than the
sources and in design-time builds) and embeds it as a resource; `HelperScript` writes it to
`LocalApplicationData/Dependinator/helpers/ts-parser-<hash>.js` on first use. Versions of the app
that run side by side each have their own script there; one that no version has started with for
30 days is deleted. So **building the
solution needs Node.js and npm** (CI runners, the devcontainer and cloud sessions have them), and
`Helper/package-lock.json` is committed.

**Node.js at run time:** in VS Code the extension passes its own runtime
(`initializationOptions.nodePath`, `process.execPath`), which the parser runs with
`ELECTRON_RUN_AS_NODE=1`, so a user needs nothing installed. The Blazor Server host looks for
`node` on the PATH (`NodeRuntime`). The browser app cannot parse at all, as with C#.

## What is a model

A **project**: a `tsconfig.json`, a `jsconfig.json`, or, for plain JavaScript without either, a
`package.json` with source files (`ModelPaths.IsTypeScript`, `ScriptProjects.Find` in
`WorkspaceFileService`). A config that another config names under `references` is part of that
one and not listed; a `package.json` is listed only when no config is beside, above or below it.
The model's display name is the project's folder (`ModelPaths.DisplayName`), since every project's
file is called the same.

The program is what the compiler makes of the config: its files and options (`project.ts`). A
config that references other projects is **one program per project** (a "part"), each compiled
with its own options, because a solution style config has no options of its own and its projects
differ in path aliases, `types`, libraries and JSX. A file belongs to the part whose config names
it; the same file is a different object in each program that reads it, so a declaration reached
from another part is found by its place in the source (`placeOf`: file, position and kind).

Two things keep that workable for a monorepo:

- **Files are parsed once** for all the programs (`libraries.ts`: the libraries, the packages'
  types and the parts' source, shared between programs whose options parse and bind them alike,
  as the editor shares them between its projects). A made-up monorepo of 31 referenced projects
  that use the DOM, Node and a 200-file core package parses in 3 s with 410 MB; without the
  sharing Node ran out of memory at 30 small projects.
- **An import of another part is that part's source**, as in the editor
  (`useSourceOfProjectReferenceRedirect`): `@acme/util` whose `package.json` names
  `dist/index.d.ts` leads to `libs/util/src`, built or not. Every other part is given to the
  program as a referenced project, also where a part's own config has no `references` (a
  workspace that leaves the order to its package manager). The configs are read once
  (`readConfig`, served to the programs through `getParsedCommandLine`).

Two options are not the config's: nothing is emitted, and where the config has no `types`, every
installed `@types` package is included (`types: ["*"]`). TypeScript 6 includes none by itself,
and then Node's modules and globals (`fs`, `process`) have no declarations to link to.

A `package.json` project takes every source file below its folder except `node_modules` and the
usual output folders (`dist`, `build`, `out`, `coverage`), as JavaScript that may hold TypeScript.
`ScriptProjects.HasSourceFiles` uses the same rule to decide whether a `package.json` is a model
(test files count there: the parser offers to include them when a project has nothing else).

## Model shape

| TypeScript / JavaScript | Node | Name |
|---|---|---|
| project | `Solution`, described by the package's `description`, its file span the entry file | the project's folder, as the model is listed (a solution's root is named after its file): `shop` |
| folder | `Folder` | `shop.src.lib` |
| source file | `SourceFile`, described by its top comment | `shop.src.cart*ts` (shown as `cart.ts`) |
| function, arrow function or function expression in a `const` | `FunctionMember` row of its file | `shop.src.main*ts.start` |
| other variable | `VariableMember` row | `shop.src.main*ts.server` |
| class / interface / enum / type alias | `ClassType` / `InterfaceType` / `EnumType` / `Type`, inside its file | `shop.src.cart*ts.Cart` |
| method, accessor, property, constructor, enum member | `MethodMember`, `PropertyMember`, `ConstructorMember` (`constructor`), `FieldMember` rows | `shop.src.cart*ts.Cart.add` |
| namespace | `Namespace` inside its file | |
| package, Node.js built-in | under `$Externals`, a scope as a level | `vscode.window`, `@angular.core`, `node.fs.readFileSync` |

- **Names:** a literal dot in a file, folder or package name is `*`, as for C# project files. `$`
  and `#` mean other things in a node name (`$Externals`, and the UI drops them), so those of
  JavaScript names (`data$`, `#count`, a route file `posts.$slug.tsx`) are written as their
  full-width forms `＄` and `＃`, which `NodeName.ToNiceText` shows as `$` and `#` again.
- **One name, two declarations:** a value and a type or namespace of the same name (`const
  Options = {…}` with `type Options = {…}`) are one node, the type's, since a member row cannot
  hold the type's members. Overloads and merged interfaces are the first declaration's node.
- **Packages:** a file under `node_modules` belongs to the package it is installed as (`@types/x`
  is `x`). A package of the same repository that `node_modules` only links to (a workspace
  package, which the compiler resolves to its real folder) is named by the nearest
  `package.json`, unless it is one of the model's own parts.
- **Private** is "not exported" for a file's declarations and `private`/`#name` for class members.
  CommonJS exports (`module.exports = { a }`, `exports.a = …`) count as exported.
- **Signatures** are TypeScript style, the way the member rows show them: `(port?: number):
  Promise<void>`, `: number`. Declared types as written, otherwise what the compiler infers (a
  literal's kind, not its value; nothing when it only knows `any`), cut at 50 characters. The
  tooltip shows a member of a script file in the same order, not the C# order.
- **Descriptions:** JSDoc for declarations. A file's is its top comment when an empty line or the
  imports follow it and it is not a license; a comment directly above the first declaration
  belongs to that declaration (Improve Comments tells the AI this).
- **Links** go from the declaration that contains a reference to the declaration it resolves to
  (through import aliases and re-exports), so `main.ts.start → cart.ts.Cart.add`. An import by
  itself is no link, only use is, except where an import is the use: a side-effect import
  (`import "./setup"`), `export … from`, a dynamic `import()` or `require()` whose result is not
  taken apart. `extends`/`implements` are inheritance links. References to a whole module, to the
  default library (`Promise`, `console`) and to a declaration's own parts make no link.
- **Tests** (`*.test.*`, `*.spec.*`, `*.e2e.*`, and files under `test`, `tests`, `__tests__`,
  `__mocks__`, `e2e`) are left out unless Settings › Include test files is on (the setting that
  includes test projects for C#).
- **Problems:** files with syntax errors, and imported packages that are not installed (run
  `npm install`), each as one summary. An import the compiler cannot resolve is "not installed"
  only when no `node_modules` folder above the file has the package: a style sheet or image of an
  installed package (`import "pkg/dist/site.css"`) is a plain link to the package, and a path
  alias (the config's `paths`, or the bundlers' `@/x`, `~/x`, `#x`) is no package at all. A
  catch-all `"*": ["src/*"]` is an alias only for what is in one of its places (`components/x`
  with a folder `src/components`; a place in `node_modules` does not count), since it matches
  every package's name too. An alias that only the bundler's config knows and that looks like a package
  (`@components/Button`) cannot be told from a missing one: it is reported, and the message says
  that such an alias belongs in the config's `paths`.
- A project's sole folder chain (`src`) is a pass-through in the diagram, like the namespaces
  that repeat an assembly's name (`PassThroughService`), so opening the project shows its files.

## In VS Code

The extension watches script and project files too. Each change message names the kinds of the
files that changed ("script", "bicep", "code"), and the UI ignores the kinds the open model is not parsed
from (`IModelService.IsParsedFrom`), because a JavaScript build writes `.js` files beside a C#
solution. With auto refresh off, a message carries only the kinds changed since the last one.
Script files under `dist`, `out`, `build` and `coverage` are not watched (what a
project builds), and nothing under `node_modules`, `bin` or `obj`; the folders are matched within
the workspace, not in the path to it. So a project that keeps source in a folder of one of these
names (`bin/cli.js`) is parsed, but a change there needs a manual refresh.

## Parked follow-ups

- **One model for the workspace** is built (Nov 13, `WORKSPACE-MODEL.md`): the sources found in
  the workspace folder side by side in one model, the user choosing which are included, so the
  extension shows beside the C# projects in this repository's own diagram. Links between
  languages (the extension starting the language server) are a later step.
- **Go**, with the same helper approach (`go/packages`).
- Vue and Svelte single-file components are not parsed (their script blocks need the frameworks'
  own compilers).
- A file's functions are rows, so calls between the functions of one file are inner lines, hidden
  until selected. Large files of only functions make large cards.
- Class expressions, object literal members and function-local declarations are not nodes; their
  references count for the enclosing declaration.
- Type-only uses count as links (a parameter type). There is no setting to leave them out.
- A package's `export =` members and re-exported third-party types resolve to the package that
  declares them, which can differ from the one imported.
- The demo models have no TypeScript model, and the browser app cannot open one unless it was
  parsed in VS Code and synced.
- `Helper/package.json` is covered by Dependabot (`.github/dependabot.yml`: weekly, minor and
  patch grouped, TypeScript's major version ignored until the native compiler's API is stable)
  and by the nightly package task. The unit tests parse the samples with the bumped compiler.
- A project whose source is under `build` or `out` and that has no config file is not found (the
  folders are skipped when a `package.json` project's files are collected).
- With auto refresh off, the extension does not learn of a manual re-parse, so switching auto
  refresh on re-parses once for changes that may already be in the model (as before for C#).
- `preserveSymlinks` projects are untried: a part used through its `node_modules` link may then
  show as a package beside its own source.

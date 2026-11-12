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
`LocalApplicationData/Dependinator/helpers/ts-parser-<hash>.js` on first use. So **building the
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

The program is what the compiler makes of the config: its files and options, with the referenced
projects' files (`project.ts`). A `package.json` project takes every source file below its folder
except `node_modules` and the usual output folders, as JavaScript that may hold TypeScript.

## Model shape

| TypeScript / JavaScript | Node | Name |
|---|---|---|
| project | `Solution`, described by the package's `description`, its file span the entry file | the package name without its scope, else the folder: `shop` |
| folder | `Folder` | `shop.src.lib` |
| source file | `SourceFile`, described by its top comment | `shop.src.cart*ts` (shown as `cart.ts`) |
| function, arrow function or function expression in a `const` | `FunctionMember` row of its file | `shop.src.main*ts.start` |
| other variable | `VariableMember` row | `shop.src.main*ts.server` |
| class / interface / enum / type alias | `ClassType` / `InterfaceType` / `EnumType` / `Type`, inside its file | `shop.src.cart*ts.Cart` |
| method, accessor, property, constructor, enum member | `MethodMember`, `PropertyMember`, `ConstructorMember` (`constructor`), `FieldMember` rows | `shop.src.cart*ts.Cart.add` |
| namespace | `Namespace` inside its file | |
| package, Node.js built-in | under `$Externals`, a scope as a level | `vscode.window`, `@angular.core`, `node.fs.readFileSync` |

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
  `npm install`), each as one summary.
- A project's sole folder chain (`src`) is a pass-through in the diagram, like the namespaces
  that repeat an assembly's name (`PassThroughService`), so opening the project shows its files.

## In VS Code

The extension watches script and project files too. Each change message names the kinds of the
files that changed ("script", "code"), and the UI ignores the kinds the open model is not parsed
from (`IModelService.IsParsedFrom`), because a JavaScript build writes `.js` files beside a C#
solution. Script files under `dist`, `out`, `build` and `coverage` are not watched (what a
project builds), and nothing under `node_modules`.

## Parked follow-ups

- **One model for the workspace** (the next piece of work, asked for by Michael on Nov 12): the
  sources found in the workspace folder put together into one model, the user choosing which are
  included, so the extension shows beside the C# projects in this repository's own diagram. Each
  parser's items are already self-contained, so combining is mostly a matter of roots and names;
  links between languages (the extension starting the language server) are a later step.
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
- `Helper/package.json` is not covered by Dependabot (the npm ecosystem is not configured); the
  nightly package task does not look at it either. Bump TypeScript by hand, with the tests.

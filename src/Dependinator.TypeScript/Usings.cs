global using Dependinator.Core.Parsing;
global using Dependinator.Core.Parsing.Sources;
global using Dependinator.Core.Utils;
global using Dependinator.Core.Utils.Logging;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dependinator.TypeScript.Tests")]
[assembly: InternalsVisibleTo("Dependinator.Lsp")]
[assembly: InternalsVisibleTo("Dependinator.Web")]
[assembly: AssemblyDescription(
    "Dependinator.TypeScript parses TypeScript and JavaScript projects into the Dependinator.Core model of files, types, functions and their dependencies. The parser itself is a Node program built on the TypeScript compiler (Helper/), which this library runs and reads the model items from."
)]

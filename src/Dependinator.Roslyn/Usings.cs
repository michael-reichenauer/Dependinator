global using Dependinator.Core.Parsing;
global using Dependinator.Core.Parsing.Sources;
global using Dependinator.Core.Utils;
global using Dependinator.Core.Utils.Logging;
global using static Dependinator.Core.Utils.Result;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dependinator.Roslyn.Tests")]
[assembly: InternalsVisibleTo("Dependinator.DemoGen")]

[assembly: AssemblyDescription(
    "Dependinator.Roslyn provides Roslyn-based source parsing that enriches the Dependinator.Core model."
)]

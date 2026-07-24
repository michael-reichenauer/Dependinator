global using Dependinator.Core.Parsing;
global using Dependinator.Core.Utils;
global using Dependinator.Core.Utils.Logging;
global using static Dependinator.Core.Utils.Result;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dependinator.Reflection.Tests")]
[assembly: InternalsVisibleTo("Dependinator.UI.Tests")]

[assembly: AssemblyDescription(
    "Dependinator.Reflection provides reflection-based parsing of compiled assemblies and solutions. Currently inactive; kept for possible future re-activation."
)]

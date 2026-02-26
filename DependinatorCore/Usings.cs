global using DependinatorCore.Utils;
global using DependinatorCore.Utils.Logging;
global using static DependinatorCore.Utils.Result;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dependinator")]
[assembly: InternalsVisibleTo("Dependinator.Lsp")]
[assembly: InternalsVisibleTo("Dependinator.Roslyn")]
[assembly: InternalsVisibleTo("Dependinator.Roslyn.Tests")]
[assembly: InternalsVisibleTo("Dependinator.Web")]
[assembly: InternalsVisibleTo("Dependinator.Wasm")]
[assembly: InternalsVisibleTo("Dependinator.Core.Tests")]
[assembly: InternalsVisibleTo("Dependinator.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

[assembly: AssemblyDescription("DependinatorCore contains the core functionality, like e.g. parsing.")]

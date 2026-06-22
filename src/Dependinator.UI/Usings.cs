global using Dependinator.Core.Utils;
global using Dependinator.Core.Utils.Logging;
global using static Dependinator.Core.Utils.Result;
global using Dependinator.UI.Shared;
global using Parsing = Dependinator.Core.Parsing;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dependinator.Wasm")]
[assembly: InternalsVisibleTo("Dependinator.Web")]
[assembly: InternalsVisibleTo("Dependinator.UI.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // DI and tests access

[assembly: AssemblyDescription("Dependinator is the UI for visualizing and exploring software dependencies.")]

global using Dependinator.Shared;
global using Dependinator.Shared.Utils;
global using Dependinator.Shared.Utils.Logging;
global using static Dependinator.Shared.Utils.Result;
global using Dependinator.Utils;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dependinator.Wasm")]
[assembly: InternalsVisibleTo("DependinatorWeb")]
[assembly: InternalsVisibleTo("Dependinator.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // DI and tests access

[assembly: AssemblyDescription("Dependinator is a tool for visualizing and exploring software dependencies.")]

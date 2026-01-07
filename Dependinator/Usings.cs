global using Dependinator.Shared;
global using DependinatorCore.Utils;
global using DependinatorCore.Utils.Logging;
global using static DependinatorCore.Utils.Result;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dependinator.Wasm")]
[assembly: InternalsVisibleTo("DependinatorWeb")]
[assembly: InternalsVisibleTo("Dependinator.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // DI and tests access

[assembly: AssemblyDescription("Dependinator is a tool for visualizing and exploring software dependencies.")]

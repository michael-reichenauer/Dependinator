global using Dependinator.Shared;
global using Dependinator.Utils;
global using Dependinator.Utils.Logging;
global using static Dependinator.Utils.Result;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dependinator.Client")]
[assembly: InternalsVisibleTo("DependinatorWeb")]
[assembly: InternalsVisibleTo("Dependinator.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // DI and tests access

[assembly: AssemblyDescription("Dependinator is a tool for visualizing and exploring software dependencies.")]

namespace Dependinator;

class RootClass { }

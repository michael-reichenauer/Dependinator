global using DependinatorCore.Utils;
global using DependinatorCore.Utils.Logging;
global using static DependinatorCore.Utils.Result;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dependinator")]
[assembly: InternalsVisibleTo("DependinatorLanguageServer")]
[assembly: InternalsVisibleTo("DependinatorRoslyn")]
[assembly: InternalsVisibleTo("DependinatorRoslyn.Tests")]
[assembly: InternalsVisibleTo("DependinatorWeb")]
[assembly: InternalsVisibleTo("DependinatorWasm")]
[assembly: InternalsVisibleTo("DependinatorCore.Tests")]
[assembly: InternalsVisibleTo("Dependinator.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

[assembly: AssemblyDescription("DependinatorCore contains the core functionality, like e.g. parsing.")]

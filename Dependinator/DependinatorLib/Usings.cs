global using Dependinator.Utils;
global using Dependinator.Utils.Logging;
global using static Dependinator.Utils.Result;

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DependinatorWeb")]
[assembly: InternalsVisibleTo("DependinatorLibTest")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]  // DI access


namespace Dependinator;
class RootClass { }

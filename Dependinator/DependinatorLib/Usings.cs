global using DependinatorLib.Utils;
global using DependinatorLib.Utils.Logging;
global using static DependinatorLib.Utils.Result;

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DependinatorWeb")]
[assembly: InternalsVisibleTo("DependinatorLibTest")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]  // DI access

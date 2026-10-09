global using Dependinator.Core.Parsing;
global using Dependinator.Core.Parsing.Sources;
global using Dependinator.Core.Utils;
global using Dependinator.Core.Utils.Logging;
using System.Reflection;
using System.Runtime.CompilerServices;

// Naming rule: inside a "namespace Dependinator.Bicep..." body the identifier "Bicep" binds to
// Dependinator.Bicep, so a qualified "Bicep.Core.X" does not compile (CS0234). Import the compiler's
// namespaces with file-top "using Bicep.Core...;" directives (they resolve from the global
// namespace) and never write "Bicep.Core.*" qualified names in code. Bicep.Core.Parsing and
// Bicep.Core.Utils are not imported: they hold twins of Dependinator's Parsing/Result names.

[assembly: InternalsVisibleTo("Dependinator.Bicep.Tests")]
[assembly: AssemblyDescription(
    "Dependinator.Bicep parses Azure Bicep deployments with the Bicep compiler library (Azure.Bicep.Core) into the Dependinator.Core model of resources, modules and their dependencies."
)]

global using Dependinator.Core.Utils;
global using Dependinator.Core.Utils.Logging;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dependinator.Lsp")]
[assembly: InternalsVisibleTo("Dependinator.Web")]
[assembly: InternalsVisibleTo("Dependinator.Claude.Tests")]
[assembly: AssemblyDescription(
    "Dependinator.Claude asks Claude, with the user's own Anthropic API key, through Anthropic's official C# SDK."
)]

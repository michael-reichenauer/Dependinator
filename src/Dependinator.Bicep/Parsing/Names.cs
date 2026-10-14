namespace Dependinator.Bicep.Parsing;

// Node names of a Bicep model. "." is the hierarchy separator in model node names, so the entry
// file's literal dot is encoded as "*" (e.g. "main*bicep", shown as "main.bicep"), the same
// encoding Dependinator.Roslyn uses for solution and module names. Bicep symbolic names are plain
// identifiers (no dots), so children are simply appended: "main*bicep.web.config".
static class Names
{
    public const string ExternalsName = "$Externals";

    public static string GetRootName(string entryPath) => Path.GetFileName(entryPath).Replace(".", "*");

    public static string Child(string parentName, string symbolName) => $"{parentName}.{symbolName}";

    // The display form of a node name ("main*bicep.web" -> "main.bicep.web"), for descriptions.
    public static string ToDisplayName(string nodeName) => nodeName.Replace("*", ".");
}

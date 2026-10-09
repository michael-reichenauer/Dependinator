using System.Text.RegularExpressions;

namespace Dependinator.Core.Shared;

// Finds the entry points among a workspace's .bicep files: the files no other .bicep file uses
// as a module (or imports), typically main.bicep. Each entry point is one model; the modules it
// references are parsed as part of it. A cheap text scan, so listing models stays fast; the real
// module resolution happens in the Bicep parser.
static class BicepEntryPoints
{
    // "module <id> '<path>'" and "import ... from '<path>'" with a local .bicep path. Registry
    // references (br:..., br/public:..., ts:...) and ARM json templates never end in ".bicep".
    static readonly Regex LocalReference = new(
        @"^\s*(?:module\s+\w+\s+|import\s+.*?\bfrom\s+)'([^'\r\n]+\.bicep)'",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    public static IReadOnlyList<string> Find(IReadOnlyList<string> bicepFiles, Func<string, string> readText)
    {
        var referenced = new HashSet<string>(PathComparer);
        foreach (var file in bicepFiles)
        {
            string text;
            try
            {
                text = readText(file);
            }
            catch (Exception)
            {
                continue; // Unreadable files are kept as entry points; the parser reports the error.
            }

            var folder = Path.GetDirectoryName(file) ?? "";
            foreach (Match match in LocalReference.Matches(text))
            {
                referenced.Add(Path.GetFullPath(Path.Combine(folder, match.Groups[1].Value)));
            }
        }

        return bicepFiles.Where(file => !referenced.Contains(Path.GetFullPath(file))).ToList();
    }

    static StringComparer PathComparer =>
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
}

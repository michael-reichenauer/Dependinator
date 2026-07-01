namespace Dependinator.Roslyn.Parsing;

static class SolutionDescriptionReader
{
    // Returns the first README.md paragraph next to the solution, or null.
    public static string? TryReadFromReadme(string solutionPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(solutionPath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                return null;

            var readme = Directory
                .EnumerateFiles(dir)
                .FirstOrDefault(f => Path.GetFileName(f).Equals("README.md", StringComparison.OrdinalIgnoreCase));
            if (readme is null)
                return null;

            return ExtractFirstParagraph(File.ReadAllText(readme));
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            Log.Exception(e, $"Failed to read solution README for {solutionPath}");
            return null;
        }
    }

    internal static string? ExtractFirstParagraph(string markdown)
    {
        var lines = markdown.Replace("\r", "").Split('\n');
        var paragraph = new List<string>();

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (paragraph.Count == 0)
            {
                // Skip preamble: blank lines, ATX headings, badge/image lines, raw HTML.
                if (
                    line.Length == 0
                    || line.StartsWith("#")
                    || line.StartsWith("![")
                    || line.StartsWith("[![")
                    || line.StartsWith("<")
                )
                    continue;
            }
            else if (line.Length == 0 || line.StartsWith("#"))
            {
                break; // end of the first paragraph
            }

            paragraph.Add(line);
        }

        // Markdown soft-wraps render as one line; join with spaces.
        var text = string.Join(" ", paragraph).Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}

using System.Text.Json;
using Dependinator.Core.Shared;

namespace Dependinator.UI.Diagrams.Ai;

// What Claude Code reports after "Fix with Claude Code": what it did (or why it changed nothing,
// and what to build and test) and the files it changed, relative to the code's folder.
public record ClaudeCodeFixReport(string Summary, IReadOnlyList<string> ChangedFiles)
{
    public const string JsonSchema = """
        {
          "type": "object",
          "properties": {
            "summary": { "type": "string", "description": "What was changed and why, or why nothing was changed; and what the user should build and test." },
            "changedFiles": { "type": "array", "items": { "type": "string" }, "description": "Every file changed or created." }
          },
          "required": ["summary", "changedFiles"],
          "additionalProperties": false
        }
        """;

    public static Result<ClaudeCodeFixReport> Parse(string json, string? codeFolder)
    {
        var parsed = Result.Catch(() => Json.Deserialize<Answer>(json) ?? throw new JsonException("No answer"));
        if (parsed is not Answer answer)
            return new Error("Claude Code's report could not be read.", parsed.Error);
        List<string> files =
        [
            .. (answer.ChangedFiles ?? [])
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => Relative(f.Trim(), codeFolder))
                .Distinct(),
        ];
        return new ClaudeCodeFixReport((answer.Summary ?? "").Trim(), files);
    }

    record Answer(string? Summary, List<string>? ChangedFiles);

    // Claude Code may list absolute paths; inside the folder they read better relative to it.
    static string Relative(string file, string? codeFolder)
    {
        if (codeFolder is null || !Path.IsPathFullyQualified(file))
            return file;
        var relative = Path.GetRelativePath(codeFolder, file);
        return relative.StartsWith("..", StringComparison.Ordinal) ? file : relative.Replace('\\', '/');
    }
}

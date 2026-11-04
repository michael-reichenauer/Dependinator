using System.Text.Json;
using System.Text.Json.Serialization;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Ai;

namespace Dependinator.UI.Diagrams.Editing;

// An AI's suggestion for a line's label (the short text drawn on the line) and its description
// (the smaller text under it), as the line label dialog shows it, with a few other labels to pick
// from instead. There is no verdict to weigh, so every suggestion reads "Suggestion".
public record LineLabelSuggestion(string Label, string Description) : IAiAnswer
{
    public AiSource Source { get; init; } = AiSource.Claude;

    public IReadOnlyList<string> Alternatives { get; init; } = [];

    public string Verdict => "Suggestion";

    // The longest label and description kept, as LineLabelPrompt asks; longer ones are cut at a word.
    public const int MaxLabelLength = 40;
    public const int MaxDescriptionLength = 120;
    const int MaxAlternatives = 3;

    public const string JsonSchema = """
        {
          "type": "object",
          "properties": {
            "label": { "type": "string", "description": "The label: one to four words, what flows across the line or what the source does with the target." },
            "description": { "type": "string", "description": "The description under the label: one short phrase with more detail, or empty." },
            "alternatives": { "type": "array", "items": { "type": "string" }, "description": "Up to three other labels, as short as the label." }
          },
          "required": ["label", "description", "alternatives"],
          "additionalProperties": false
        }
        """;

    public static Result<LineLabelSuggestion> Parse(string json, AiSource source = AiSource.Claude)
    {
        var parsed = Result.Catch(() => Json.Deserialize<Answer>(json) ?? throw new JsonException("No answer"));
        if (parsed is not Answer answer)
            return new Error($"{source.Name()}'s answer could not be read.", parsed.Error);
        return Create(answer.Label, answer.Description, answer.Alternatives ?? [], source);
    }

    // An AI chat's answer to the copied prompt, pasted back: its "Label:", "Description:" and
    // "Alternatives:" lines (alternatives separated by commas, semicolons or lines).
    public static Result<LineLabelSuggestion> ParseText(string text)
    {
        var fields = AiAnswerText.Fields(text, ["Label", "Description", "Alternatives"]);
        if (!fields.TryGetValue("label", out var label))
            return new Error("The answer has no \"Label:\" line. Paste the whole answer.");
        var alternatives = fields.TryGetValue("alternatives", out var list)
            ? list.Split([',', ';', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : [];
        return Create(label, fields.GetValueOrDefault("description"), alternatives, AiSource.Chat);
    }

    static Result<LineLabelSuggestion> Create(
        string? label,
        string? description,
        IEnumerable<string> alternatives,
        AiSource source
    )
    {
        var labelText = Clean(label, MaxLabelLength);
        if (labelText.Length == 0)
            return new Error($"{source.Name()} gave an empty label.");
        return new LineLabelSuggestion(labelText, Clean(description, MaxDescriptionLength))
        {
            Source = source,
            Alternatives =
            [
                .. alternatives
                    .Select(a => Clean(a, MaxLabelLength))
                    .Where(a => a.Length > 0 && !a.Equals(labelText, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(MaxAlternatives),
            ],
        };
    }

    // One line without list marks, emphasis or quotes, cut at a word when too long.
    internal static string Clean(string? text, int maxLength)
    {
        var line = string.Join(" ", (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        line = line.TrimStart('-', '•', ' ').Trim('*', '_', '`', ' ').Trim('"', '“', '”', '\'', ' ');
        if (line.Length <= maxLength)
            return line;
        var wordEnd = line.LastIndexOf(' ', maxLength);
        return line[..(wordEnd > 0 ? wordEnd : maxLength)].TrimEnd(',', ';', ' ') + "…";
    }

    record Answer(
        [property: JsonPropertyName("label")] string? Label,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("alternatives")] string[]? Alternatives
    );
}

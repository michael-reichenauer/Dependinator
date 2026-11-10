using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams.Editing;

// One suggested label: the line by its number in the prompt, the label and the description under
// it. Id and the names are the line Dependinator found for the number (LineLabelsSuggestion.Resolve);
// a label whose number is no line is left out.
public record SuggestedLineLabel(int Number, string Label, string Description)
{
    public LineId? Id { get; init; }

    public string SourceName { get; init; } = "";

    public string TargetName { get; init; } = "";

    // The ends' full names, where the short ones are not enough to tell lines apart.
    public string LongName { get; init; } = "";
}

// An AI's labels for several lines at once (LineLabelsPrompt), as the dialog lists them for the
// user to tick; asked in one question so the labels share a style.
public record LineLabelsSuggestion(IReadOnlyList<SuggestedLineLabel> Labels) : IAiAnswer
{
    public AiSource Source { get; init; } = AiSource.Claude;

    // How many labels the AI gave that are for no line (a number not in the list, or given twice).
    public int LeftOutCount { get; init; }

    public string Verdict => Labels.Count == 1 ? "1 label" : $"{Labels.Count} labels";

    // The fields of LineLabelsPrompt's answer format; structured output makes the JSON match it.
    public const string JsonSchema = """
        {
          "type": "object",
          "properties": {
            "labels": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "line": { "type": "integer", "description": "The number of the line, as listed." },
                  "label": { "type": "string", "description": "The label: one to four words, what flows across the line or what the source does with the target." },
                  "description": { "type": "string", "description": "The description under the label: one short phrase with more detail, or empty." }
                },
                "required": ["line", "label", "description"],
                "additionalProperties": false
              }
            }
          },
          "required": ["labels"],
          "additionalProperties": false
        }
        """;

    public static Result<LineLabelsSuggestion> Parse(string json, AiSource source = AiSource.Claude)
    {
        var parsed = Result.Catch(() => Json.Deserialize<Answer>(json) ?? throw new JsonException("No answer"));
        if (parsed is not Answer answer)
            return new Error($"{source.Name()}'s answer could not be read.", parsed.Error);
        return Create((answer.Labels ?? []).Select(l => (l.Line, l.Label, l.Description)), source);
    }

    // "3. reads secrets | the connection strings", list marks, emphasis and a "Line" before the
    // number aside.
    static readonly Regex LabelLine = new(
        @"^[\s>*_#-]*(?:line\s*)?(?<number>\d{1,4})[*_]*\s*[.:)\]][*_]*\s*(?<text>\S.*)$",
        RegexOptions.IgnoreCase
    );

    // An AI chat's answer to the copied prompt, pasted back: a line per label, "<number>. <label>
    // | <description>" (the description may be left out).
    public static Result<LineLabelsSuggestion> ParseText(string text)
    {
        List<(int, string?, string?)> labels = [];
        foreach (var line in text.Replace("\r", "").Split('\n'))
        {
            if (LabelLine.Match(line) is not { Success: true } match)
                continue;
            var parts = match.Groups["text"].Value.Split('|', 2);
            labels.Add((int.Parse(match.Groups["number"].Value), parts[0], parts.Length > 1 ? parts[1] : ""));
        }
        return labels.Count == 0
            ? new Error("The answer has no \"<number>. <label>\" lines, so it could not be read.")
            : Create(labels, AiSource.Chat);
    }

    static Result<LineLabelsSuggestion> Create(
        IEnumerable<(int Line, string? Label, string? Description)> answers,
        AiSource source
    )
    {
        List<SuggestedLineLabel> labels =
        [
            .. answers
                .Select(a => new SuggestedLineLabel(
                    a.Line,
                    LineLabelSuggestion.Clean(a.Label, LineLabelSuggestion.MaxLabelLength),
                    LineLabelSuggestion.Clean(a.Description, LineLabelSuggestion.MaxDescriptionLength)
                ))
                .Where(l => l.Label.Length > 0),
        ];
        return labels.Count == 0
            ? new Error($"{source.Name()} gave no labels.")
            : new LineLabelsSuggestion(labels) { Source = source };
    }

    // The labels whose numbers are lines of the question that are still in the model, each line
    // once (its first label counts), in the order of the lines.
    internal static Result<LineLabelsSuggestion> Resolve(
        LineLabelsSuggestion suggestion,
        IModel model,
        IReadOnlyList<LineId> lineIds
    )
    {
        HashSet<int> seen = [];
        List<SuggestedLineLabel> labels = [];
        foreach (var label in suggestion.Labels)
        {
            if (
                label.Number < 1
                || label.Number > lineIds.Count
                || !model.Lines.TryGetValue(lineIds[label.Number - 1], out var line)
                || !seen.Add(label.Number)
            )
                continue;
            labels.Add(
                label with
                {
                    Id = line.Id,
                    SourceName = line.Source.DisplayName,
                    TargetName = line.Target.DisplayName,
                    LongName = $"{line.Source.LongName} → {line.Target.LongName}",
                }
            );
        }
        if (labels.Count == 0)
            return new Error($"None of the labels {suggestion.Source.Name()} gave is for a line in the list.");
        return suggestion with
        {
            Labels = [.. labels.OrderBy(l => l.Number)],
            LeftOutCount = suggestion.Labels.Count - labels.Count,
        };
    }

    record Answer([property: JsonPropertyName("labels")] List<AnswerLabel>? Labels);

    record AnswerLabel(
        [property: JsonPropertyName("line")] int Line,
        [property: JsonPropertyName("label")] string? Label,
        [property: JsonPropertyName("description")] string? Description
    );
}

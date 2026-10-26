using System.Text.RegularExpressions;

namespace Dependinator.UI.Diagrams.Ai;

// Where an AI answer in a review came from: Claude with the user's API key, Claude Code on this
// computer (the experimental local route), or another AI chat, pasted back by the user.
public enum AiSource
{
    Claude,
    ClaudeCode,
    Chat,
}

// An answer a review dialog shows (AiOpinion): where it came from, and its verdict, one of a few
// words ("Accept", "Break"), for the answer's label and to tell when answers differ.
public interface IAiAnswer
{
    AiSource Source { get; }
    string Verdict { get; }
}

public static class AiSourceExtensions
{
    // As the subject of a sentence: "Claude Code suggests breaking it".
    public static string Name(this AiSource source) =>
        source switch
        {
            AiSource.Claude => "Claude",
            AiSource.ClaudeCode => "Claude Code",
            _ => "Your AI chat",
        };

    // On an answer's chip: "Chat · Accept".
    public static string ShortName(this AiSource source) =>
        source switch
        {
            AiSource.Claude => "Claude",
            AiSource.ClaudeCode => "Claude Code",
            _ => "Chat",
        };
}

// Reads an AI chat's answer to a copied prompt, pasted back by the user: the lines of the prompt's
// answer format ("Verdict: Break", "Why: ..."), wherever they are in the text, markdown emphasis
// and list marks aside. A field runs until the next one; the first of a repeated label counts.
public static class AiAnswerText
{
    // The fields by label, lower case. Labels are matched case-insensitively at a line start.
    public static Dictionary<string, string> Fields(string text, IReadOnlyList<string> labels)
    {
        var pattern = new Regex(
            @"^[ \t>*_#-]*(?<label>"
                + string.Join("|", labels.OrderByDescending(l => l.Length).Select(Regex.Escape))
                + @")[ \t*_]*:[ \t*_]*",
            RegexOptions.IgnoreCase | RegexOptions.Multiline
        );
        Dictionary<string, string> fields = [];
        var matches = pattern.Matches(text);
        for (var i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index + matches[i].Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            fields.TryAdd(matches[i].Groups["label"].Value.ToLowerInvariant(), Clean(text[start..end]));
        }
        return fields;
    }

    // The verdict word that comes first in a field ("Break (do not accept it)" is Break), as
    // written in words (case as given), or null.
    public static string? Verdict(string field, IReadOnlyList<string> words)
    {
        var match = Regex.Match(
            field,
            @"\b(" + string.Join("|", words.Select(Regex.Escape)) + @")\b",
            RegexOptions.IgnoreCase
        );
        return match.Success ? words.First(w => w.Equals(match.Value, StringComparison.OrdinalIgnoreCase)) : null;
    }

    // A field's text without the format's "(only if Accept)" note, emphasis marks or quotes
    // around it; "N/A" or a dash for a field that does not apply is empty.
    static string Clean(string value)
    {
        var text = OnlyIfNote.Replace(value.Trim(), "").Trim().Trim('*', '_').Trim();
        if (text.Length > 1 && text[0] is '"' or '“' && text[^1] is '"' or '”')
            text = text[1..^1].Trim();
        return text is "N/A" or "n/a" or "-" or "—" or "–" ? "" : text;
    }

    static readonly Regex OnlyIfNote = new(@"^\(only if [a-z]+\)", RegexOptions.IgnoreCase);
}

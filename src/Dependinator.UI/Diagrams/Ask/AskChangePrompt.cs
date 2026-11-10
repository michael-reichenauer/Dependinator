using System.Text;
using Dependinator.UI.Modeling.Models;
using static Dependinator.UI.Diagrams.Ai.PromptParts;

namespace Dependinator.UI.Diagrams.Ask;

// The prompt for an AI that can change the code (Claude Code, Copilot's agent, or another the
// user pastes it into), to make the change an Ask answer proposed: the change, the question and
// the answer it comes from, the files of the nodes it is about, and the architecture rules to
// keep. It shows no source, since such an AI reads the code itself.
static class AskChangePrompt
{
    internal const int MaxFiles = 25;
    internal const int MaxRules = 30;

    public static string Build(IModel model, ModelAnswer answer, AskContext context)
    {
        if (answer.Change == "")
            return "";

        var text = new StringBuilder();
        text.AppendLine(
            "Please make a change to this codebase. The user asked a question about it in Dependinator, a tool "
                + "that maps the structure and dependencies of the code, and the answer proposed the change."
        );
        text.AppendLine().AppendLine("## The change").AppendLine();
        text.AppendLine(answer.Change);

        text.AppendLine().AppendLine("## The question and the answer that proposed it").AppendLine();
        var about = AskModelPrompt.AboutNames(model, context);
        text.AppendLine($"**Question{(about != "" ? $" (about {about})" : "")}:** {answer.Question.Trim()}");
        text.AppendLine().AppendLine($"**Answer:** {answer.Text.Trim()}");

        var files = Files(model, answer, context);
        if (files.Count > 0)
        {
            text.AppendLine().AppendLine("## Where to look").AppendLine();
            text.AppendLine("The code of what the question and the answer are about:");
            foreach (var (name, file) in files.Take(MaxFiles))
                text.AppendLine($"- `{name}`: {file}");
            if (files.Count > MaxFiles)
                text.AppendLine($"- … and {files.Count - MaxFiles} more");
        }

        if (model.Rules.Count > 0)
        {
            text.AppendLine().AppendLine("## Architecture rules to keep").AppendLine();
            text.AppendLine("The change must not add a dependency that breaks one of the codebase's rules:");
            foreach (var rule in model.Rules.Take(MaxRules))
                text.AppendLine($"- {rule.Describe(name => name)}");
            if (model.Rules.Count > MaxRules)
                text.AppendLine($"- … and {model.Rules.Count - MaxRules} more");
        }

        text.AppendLine().Append(TaskSection);
        return text.ToString();
    }

    // The nodes the question was about, then the ones the answer names, each with the file its
    // code is in; a node without a file of its own (a namespace, say) is left out.
    static List<(string Name, string File)> Files(IModel model, ModelAnswer answer, AskContext context)
    {
        var nodes = AskModelPrompt
            .ContextNodes(model, context)
            .Concat(answer.Links.Values.Select(l => model.Nodes.GetValueOrDefault(l.Id)).OfType<Node>())
            .Distinct();
        List<(string, string)> files = [];
        foreach (var node in nodes)
        {
            if (node.FileSpan is { } span && span.Path != "")
                files.Add((node.Name, DisplayPath(model, span.Path)));
        }
        return files;
    }

    const string TaskSection = """
        ## Your task

        1. Read the code first, and check what the answer says about it. It was written from the dependency model and may be wrong in details: if the change does not fit the code, say so, and propose another before editing.
        2. Make the change without changing behavior, and no larger than it needs to be.
        3. Update the code that refers to what you moved, split or renamed.
        4. Build, and run the tests.
        5. Summarize what you changed. Dependinator shows the new structure after its next parse.

        """;
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dependinator.Core.Claude;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using static Dependinator.UI.Diagrams.PromptParts;

namespace Dependinator.UI.Diagrams;

// An AI's answer to a question about the whole model (the Ask panel): text with a little markdown,
// where node names are in backticks, and the nodes the answer is about, each with its part in it.
// Links maps each name the answer gives (in backticks or in Nodes) that is in the model to its
// node and display names, so the panel can show it as a link; it is filled when the answer arrives.
public record ModelAnswer(string Text, IReadOnlyList<AnswerNode> Nodes) : IAiAnswer
{
    public AiSource Source { get; init; } = AiSource.Claude;

    public string Question { get; init; } = "";

    public string Verdict => "Answer";

    public IReadOnlyDictionary<string, NodeLink> Links { get; init; } = new Dictionary<string, NodeLink>();

    // For Claude with the API key: the tool calls the answer took, and its cost in US dollars.
    public int? ToolCalls { get; init; }
    public decimal? CostUsd { get; init; }

    // The fields of AskModelPrompt's answer format; structured output makes the JSON match it.
    public const string JsonSchema = """
        {
          "type": "object",
          "properties": {
            "answer": {
              "type": "string",
              "description": "The answer, as plain text with a little markdown; node names in backticks, exactly as the tools give them."
            },
            "nodes": {
              "type": "array",
              "description": "The nodes the answer is about, the most important first.",
              "items": {
                "type": "object",
                "properties": {
                  "name": { "type": "string", "description": "The node's name, exactly as the tools give it." },
                  "why": { "type": "string", "description": "A few words on its part in the answer." }
                },
                "required": ["name", "why"],
                "additionalProperties": false
              }
            }
          },
          "required": ["answer", "nodes"],
          "additionalProperties": false
        }
        """;

    public static Result<ModelAnswer> Parse(string json, AiSource source = AiSource.Claude)
    {
        var parsed = Result.Catch(() => Json.Deserialize<Answer>(json) ?? throw new JsonException("No answer"));
        if (parsed is not Answer answer)
            return new Error($"{source.Name()}'s answer could not be read.", parsed.Error);
        if (string.IsNullOrWhiteSpace(answer.Text))
            return new Error($"{source.Name()} gave an empty answer.");
        List<AnswerNode> nodes =
        [
            .. (answer.Nodes ?? [])
                .Where(n => !string.IsNullOrWhiteSpace(n.Name))
                .Select(n => new AnswerNode(n.Name!.Trim().Trim('`'), (n.Why ?? "").Trim()))
                .DistinctBy(n => n.Name)
                .Take(AskModelPrompt.MaxNodes),
        ];
        return new ModelAnswer(answer.Text.Trim(), nodes) { Source = source };
    }

    // The names the answer gives: its code spans (which may name a node), and its nodes.
    public IEnumerable<string> Names() =>
        SimpleMarkdown
            .Parse(Text)
            .SelectMany(b => b.Lines)
            .SelectMany(l => l)
            .Where(s => s.IsCode)
            .Select(s => s.Text.Trim())
            .Concat(Nodes.Select(n => n.Name))
            .Where(n => n != "")
            .Distinct();

    public NodeLink? LinkOf(string name) => Links.GetValueOrDefault(name.Trim());

    record Answer([property: JsonPropertyName("answer")] string? Text, List<AnswerItem>? Nodes);

    record AnswerItem(string? Name, string? Why);
}

public record AnswerNode(string Name, string Why);

// A node an answer names: the full names the tools give are long ("Dependinator*UI*dll.Dependinator.
// UI.App.AppBar"), so the panel shows the node's short name in the text and its long one in lists.
public record NodeLink(NodeId Id, string ShortName, string LongName);

// The prompt that asks an AI a question about the model open in the diagram. It holds only an
// overview (the model's size and top level); the AI finds the rest with Dependinator's tools,
// which answer on the model as it is now, and reads the code where it can.
// An earlier question in the panel's conversation and its answer's text.
record AskExchange(string Question, string Answer);

static class AskModelPrompt
{
    internal const int MaxNodes = 12;
    const int MaxTopLevel = 40;

    // A follow-up gets the last few exchanges, each answer cut to a length that keeps the prompt
    // small; the tools can look up again what was cut.
    internal const int MaxEarlier = 3;
    internal const int MaxEarlierAnswerLength = 2000;

    public static string Build(IModel model, string question, IReadOnlyList<AskExchange> earlier)
    {
        var topLevel = model
            .Root.Children.Where(n => !NodeViewPolicy.IsAnnotation(n))
            .OrderBy(n => n.Name, StringComparer.Ordinal)
            .ToList();
        if (topLevel.Count == 0)
            return "";

        var text = new StringBuilder();
        text.AppendLine("# A question about a dependency model").AppendLine();
        text.AppendLine(
            "Dependinator shows a codebase as a dependency model: containers (solutions, projects, namespaces, "
                + "modules), the types and resources in them, their members, and the links between them. The user "
                + "has a model open and asks a question about it."
        );

        var name = model.Path != "" ? $": {DisplayPath(model, model.Path)}" : "";
        text.AppendLine().AppendLine($"## The model{name}").AppendLine();
        text.AppendLine(
            $"{model.Nodes.Count - 1} nodes, {model.Links.Count} links, {model.Rules.Count} architecture rules. "
                + "The top level:"
        );
        text.AppendLine();
        foreach (var node in topLevel.Take(MaxTopLevel))
        {
            var title = node.LongName != node.Name ? $", titled \"{node.LongName}\"" : "";
            text.AppendLine($"- `{node.Name}` ({Kind(node.Type)}{title}){Described(node)}");
        }
        if (topLevel.Count > MaxTopLevel)
            text.AppendLine($"- … and {topLevel.Count - MaxTopLevel} more");

        var recent = earlier.TakeLast(MaxEarlier).ToList();
        if (recent.Count > 0)
        {
            text.AppendLine().AppendLine("## Earlier in this conversation").AppendLine();
            if (earlier.Count > recent.Count)
            {
                var left = earlier.Count - recent.Count;
                text.AppendLine($"({left} earlier {(left == 1 ? "question" : "questions")} left out.)").AppendLine();
            }
            foreach (var exchange in recent)
            {
                text.AppendLine($"**Question:** {exchange.Question.Trim()}").AppendLine();
                text.AppendLine($"**Answer:** {Cut(exchange.Answer.Trim())}").AppendLine();
            }
            text.AppendLine("## The question (it may follow up on the conversation above)").AppendLine();
        }
        else
        {
            text.AppendLine().AppendLine("## The question").AppendLine();
        }
        text.AppendLine(question.Trim());
        text.AppendLine().Append(TaskSection);
        return text.ToString();
    }

    static string Cut(string answer) =>
        answer.Length <= MaxEarlierAnswerLength ? answer : answer[..MaxEarlierAnswerLength] + " … (cut)";

    static readonly string TaskSection = $"""
        ## How to answer

        Find out with Dependinator's tools before you answer: search_nodes to find the nodes the question is about, get_dependencies for what a node uses and what uses it, find_paths for why one part depends on another, list_children to go into a node, list_cycles and list_rules for its problems, read_source for a node's code when the names and dependencies are not enough. Use only as many calls as the question needs.

        Answer the question directly, in a few short paragraphs or a list: the answer is shown in a narrow panel beside the diagram. Write every node you mention in backticks, by its name exactly as the tools give it (such as `Dependinator.Core.Parsing.ParserService`), so the user can click it to go to the node. Under nodes, list the nodes the answer is about, at most {MaxNodes}, the most important first. If the model cannot answer the question, say so, and what would.

        """;
}

// "Ask" (AskPanel): a question about the open model for an AI, which finds the answer with
// Dependinator's model tools and names the nodes it is about, as links in the panel. Asked through
// Claude with the user's API key (the host runs the tool-use loop, IClaudeService.
// AskWithModelToolsAsync), or Claude Code on this computer (Settings › Use Claude Code); both get
// the tools through the app's MCP server. The panel sits top-right with the analysis panels, so
// opening it closes them.
interface IAskModelService
{
    // Whether this host can ask an AI: the web host and VS Code, not the standalone browser app.
    bool IsAvailable { get; }

    // The routes the user can ask through now: Claude, and Claude Code where it is turned on
    // (always in test mode).
    Task<IReadOnlyList<AiSource>> GetRoutesAsync();

    // The prompt for the question, a follow-up on the earlier ones; empty when there is no model.
    string GetPrompt(string question, IReadOnlyList<AskExchange> earlier);

    Task<Result<ModelAnswer>> AskAsync(
        string question,
        IReadOnlyList<AskExchange> earlier,
        AiSource route,
        Action<SuggestStep> onStep
    );

    void Toggle();

    void Close();
}

[Scoped]
class AskModelService(
    IModelMgr modelMgr,
    IModelService modelService,
    IAiService aiService,
    IViewOptions viewOptions,
    IPathFinderService pathFinderService,
    IApplicationEvents applicationEvents,
    IModelQueries modelQueries
) : IAskModelService
{
    public bool IsAvailable => aiService.IsAvailable;

    public async Task<IReadOnlyList<AiSource>> GetRoutesAsync() =>
        !IsAvailable ? []
        : Dependinator.Core.Build.IsTestMode || await aiService.IsClaudeCodeEnabledAsync()
            ? [AiSource.Claude, AiSource.ClaudeCode]
        : [AiSource.Claude];

    public string GetPrompt(string question, IReadOnlyList<AskExchange> earlier)
    {
        using var model = modelMgr.UseModel();
        return AskModelPrompt.Build(model, question, earlier);
    }

    public async Task<Result<ModelAnswer>> AskAsync(
        string question,
        IReadOnlyList<AskExchange> earlier,
        AiSource route,
        Action<SuggestStep> onStep
    )
    {
        // The e2e tests exercise the panel without a key or Claude Code: two lookups, as an agent
        // would make them through the tools, and a canned answer.
        if (Dependinator.Core.Build.IsTestMode)
        {
            onStep(route is AiSource.ClaudeCode ? SuggestStep.AskingClaudeCode : SuggestStep.AskingClaude);
            var lookups = new ScopeModelQueries(modelQueries);
            await lookups.GetModelAsync();
            await lookups.SearchNodesAsync(question);
            await Task.Delay(TestModeAnswerDelay);
            return WithLinks(TestModeAnswer(route, earlier.Count) with { Question = question });
        }

        // AiService's own error for an empty prompt is about a review dialog.
        var prompt = GetPrompt(question, earlier);
        if (prompt == "")
            return new Error("There is no model to ask about yet: open or parse one first.");
        Task<string> GetPromptAsync() => Task.FromResult(prompt);

        if (route is AiSource.ClaudeCode)
        {
            // The setting may have been turned off since the panel offered the route.
            if (!await aiService.IsClaudeCodeEnabledAsync())
                return new Error("Claude Code is turned off. Turn it on in Settings › Use Claude Code (Experimental).");
            var json = await aiService.AskAsync(
                route,
                GetPromptAsync,
                ModelAnswer.JsonSchema,
                onStep,
                withModelTools: true
            );
            return json is string text ? Read(text, route, question) : json.Error;
        }

        Result<ToolsAnswer> answered;
        try
        {
            answered = await aiService.AskWithModelToolsAsync(GetPromptAsync, ModelAnswer.JsonSchema, onStep);
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            return new Error($"Could not ask Claude: {e.Message}", e);
        }
        if (answered is not ToolsAnswer toolsAnswer)
            return answered.Error;
        var read = Read(toolsAnswer.Json, route, question);
        return read is ModelAnswer answer
            ? answer with
            {
                ToolCalls = toolsAnswer.ToolCalls,
                CostUsd = toolsAnswer.CostUsd,
            }
            : read.Error;
    }

    Result<ModelAnswer> Read(string json, AiSource route, string question)
    {
        var parsed = ModelAnswer.Parse(json, route);
        return parsed is ModelAnswer answer ? WithLinks(answer with { Question = question }) : parsed.Error;
    }

    // The names in the answer that are nodes, as the tools take them (a name, or a unique title or
    // alias): those become links.
    ModelAnswer WithLinks(ModelAnswer answer)
    {
        using var model = modelMgr.UseModel();
        Dictionary<string, NodeLink> links = [];
        foreach (var name in answer.Names())
        {
            if (ModelQueries.FindNode(model, name) is Node node)
                links[name] = new NodeLink(node.Id, node.ShortName, node.LongName);
        }
        return answer with { Links = links };
    }

    public void Toggle()
    {
        if (viewOptions.IsAskShown)
        {
            Close();
            return;
        }
        // The analysis panels share the top-right corner; their highlighted lines are baked into
        // the tiles.
        if (viewOptions.IsCyclesShown || viewOptions.IsRulesShown)
        {
            viewOptions.SetCyclesShown(false);
            viewOptions.SetRulesShown(false);
            modelService.ClearCache();
        }
        viewOptions.SetHiddenNodesShown(false);
        pathFinderService.Close();
        viewOptions.SetAskShown(true);
        applicationEvents.TriggerUIStateChanged();
    }

    public void Close()
    {
        viewOptions.SetAskShown(false);
        applicationEvents.TriggerUIStateChanged();
    }

    // Names the first two top-level nodes, one twice (in the text and in the nodes), and a node
    // that is not in the model, which stays plain code.
    ModelAnswer TestModeAnswer(AiSource route, int earlierCount)
    {
        var followUp = earlierCount > 0 ? $" A follow-up, after {earlierCount} answered." : "";
        List<Node> top;
        using (var model = modelMgr.UseModel())
            top = [.. model.Root.Children.Where(n => !NodeViewPolicy.IsAnnotation(n)).OrderBy(n => n.Name).Take(2)];
        var names = top.Select(n => n.Name).ToList();
        var text = names.Count switch
        {
            0 => $"Test mode: {route.Name()} was not asked.{followUp} The model is empty.",
            _ =>
                $"Test mode: {route.Name()} was not asked.{followUp}\n\n- {string.Join("\n- ", names.Select(n => $"`{n}` is at the top level"))}\n- `No.Such.Node` is not in the model",
        };
        return new ModelAnswer(text, [.. names.Select(n => new AnswerNode(n, "Test mode"))])
        {
            Source = route,
            ToolCalls = route is AiSource.Claude ? 2 : null,
            CostUsd = route is AiSource.Claude ? 0.0123m : null,
        };
    }

    static readonly TimeSpan TestModeAnswerDelay = TimeSpan.FromMilliseconds(800);
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dependinator.Core.Claude;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Diagrams.Navigation;
using Dependinator.UI.Diagrams.NodeHelp;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using static Dependinator.UI.Diagrams.Ai.PromptParts;

namespace Dependinator.UI.Diagrams.Ask;

// An AI's answer to a question about the whole model (the Ask panel): text with a little markdown,
// where node names are in backticks, and the nodes the answer is about, each with its part in it.
// Links maps each name the answer gives (in backticks or in Nodes) that is in the model to its
// node and display names, so the panel can show it as a link; it is filled when the answer arrives.
public record ModelAnswer(string Text, IReadOnlyList<AnswerNode> Nodes) : IAiAnswer
{
    public AiSource Source { get; init; } = AiSource.Claude;

    public string Question { get; init; } = "";

    public string Verdict => "Answer";

    // The change to the code the answer proposes, in one sentence ("Move loading and saving from
    // ModelService to a new ModelStore"); "" when it proposes none. The panel offers to have an
    // AI make it (AskChangePrompt).
    public string Change { get; init; } = "";

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
            },
            "change": {
              "type": "string",
              "description": "If the answer proposes a concrete change to the code, the change in one imperative sentence; otherwise an empty string."
            }
          },
          "required": ["answer", "nodes", "change"],
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
        return new ModelAnswer(answer.Text.Trim(), nodes) { Source = source, Change = Line(answer.Change) };
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

    // The proposed change on one line; "none" and the like mean there is none.
    static string Line(string? change)
    {
        var line = string.Join(" ", (change ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return line.TrimEnd('.').ToLowerInvariant() is "none" or "n/a" or "no change" ? "" : line;
    }

    record Answer([property: JsonPropertyName("answer")] string? Text, List<AnswerItem>? Nodes, string? Change);

    record AnswerItem(string? Name, string? Why);
}

public record AnswerNode(string Name, string Why);

// A node an answer names: the full names the tools give are long ("Dependinator*UI*dll.Dependinator.
// UI.App.AppBar"), so the panel shows the node's short name in the text and its long one in lists.
public record NodeLink(NodeId Id, string ShortName, string LongName);

// An earlier question in the panel's conversation and its answer's text; About names the nodes
// it was asked about ("" for the whole model).
record AskExchange(string Question, string Answer, string About = "");

// A request to ask about a node, from About Node or a node's menu.
record AskRequest(NodeId NodeId, string Question);

// What a question is about: the nodes selected in the diagram, alone or with everything inside
// them. With no nodes it is about the whole model.
record AskContext(IReadOnlyList<NodeId> NodeIds, bool IsBelow)
{
    public static readonly AskContext Model = new([], false);
}

// The prompt that asks an AI a question about the model open in the diagram. It holds only an
// overview (the model's size and top level) and the nodes the question is about, when the user
// selected some; the AI finds the rest with Dependinator's tools, which answer on the model as it
// is now, and reads the code where it can.
static class AskModelPrompt
{
    internal const int MaxNodes = 12;
    const int MaxTopLevel = 40;

    // A follow-up gets the last few exchanges, each answer cut to a length that keeps the prompt
    // small; the tools can look up again what was cut.
    internal const int MaxEarlier = 3;
    internal const int MaxEarlierAnswerLength = 2000;

    // The children listed of each node the question is about, when it is asked with what is inside.
    internal const int MaxContextNodes = 12;
    internal const int MaxInside = 30;

    public static string Build(
        IModel model,
        string question,
        IReadOnlyList<AskExchange> earlier,
        AskContext? context = null
    )
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

        AppendContext(text, model, context ?? AskContext.Model);

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
                var about = exchange.About != "" ? $" (about {exchange.About})" : "";
                text.AppendLine($"**Question{about}:** {exchange.Question.Trim()}").AppendLine();
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

    // The nodes the user selected for the question, each with what the model knows of it, so "it"
    // and "these" in the question are clear and the AI knows where to start looking.
    static void AppendContext(StringBuilder text, IModel model, AskContext context)
    {
        var nodes = ContextNodes(model, context);
        if (nodes.Count == 0)
            return;
        var isOne = nodes.Count == 1;
        var isInside = context.IsBelow && nodes.Any(HasInside);
        var scope = isInside
            ? isOne
                ? ", with everything inside it"
                : ", with everything inside them"
            : "";
        text.AppendLine().AppendLine("## What the question is about").AppendLine();
        text.AppendLine(
            isOne
                ? $"The user selected this node in the diagram and asks about it{scope} (\"it\" and \"this\" in the question mean the node):"
                : $"The user selected these {nodes.Count} nodes in the diagram and asks about them{scope} (\"they\" and \"these\" in the question mean the nodes):"
        );
        text.AppendLine();
        foreach (var node in nodes.Take(MaxContextNodes))
        {
            var facts = NodePrompt.Facts(node);
            var title = node.LongName != node.Name ? $", titled \"{node.LongName}\"" : "";
            text.AppendLine(
                $"- `{node.Name}` ({Kind(node.Type)}{title}){Described(node)}. {facts.Contents}; "
                    + $"uses {Types(facts.UsesCount)} outside it, used by {Types(facts.UsedByCount)}."
            );
            if (!context.IsBelow)
                continue;
            var inside = node
                .Children.Where(n => !NodeViewPolicy.IsAnnotation(n))
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .ToList();
            if (inside.Count == 0)
                continue;
            var names = string.Join(", ", inside.Take(MaxInside).Select(n => $"`{n.ShortName}`"));
            var more = inside.Count > MaxInside ? $", … and {inside.Count - MaxInside} more" : "";
            text.AppendLine($"  Directly inside: {names}{more}");
        }
        if (nodes.Count > MaxContextNodes)
            text.AppendLine($"- … and {nodes.Count - MaxContextNodes} more selected nodes");
        text.AppendLine();
        text.AppendLine(
            isInside ? "Look these nodes and what is inside them up with the tools, by these names, before you answer."
            : nodes.Any(HasInside)
                ? "Look these nodes up with the tools, by these names, before you answer. The question is about the nodes themselves, not about each thing inside them."
            : "Look these nodes up with the tools, by these names, before you answer."
        );
    }

    // Whether the node has anything inside to ask about; notes and zones do not count.
    internal static bool HasInside(Node node) => node.Children.Any(n => !NodeViewPolicy.IsAnnotation(n));

    // The context's nodes by their full names, as a follow-up's prompt names what an earlier
    // question was about.
    internal static string AboutNames(IModel model, AskContext context)
    {
        var nodes = ContextNodes(model, context);
        var names = string.Join(", ", nodes.Take(MaxContextNodes).Select(n => $"`{n.Name}`"));
        return nodes.Count > MaxContextNodes ? $"{names} and {nodes.Count - MaxContextNodes} more" : names;
    }

    // The nodes of the context that are (still) in the model; notes and zones are no code.
    internal static List<Node> ContextNodes(IModel model, AskContext context) =>
        [
            .. context
                .NodeIds.Select(id => model.Nodes.GetValueOrDefault(id))
                .OfType<Node>()
                .Where(n => !n.IsRoot && !NodeViewPolicy.IsAnnotation(n)),
        ];

    static string Types(int count) => count == 1 ? "1 type" : $"{count} types";

    static string Cut(string answer) =>
        answer.Length <= MaxEarlierAnswerLength ? answer : answer[..MaxEarlierAnswerLength] + " … (cut)";

    static readonly string TaskSection = $"""
        ## How to answer

        Find out with Dependinator's tools before you answer: search_nodes to find the nodes the question is about, get_dependencies for what a node uses and what uses it, find_paths for why one part depends on another, list_children to go into a node, list_cycles and list_rules for its problems, read_source for a node's code when the names and dependencies are not enough. Use only as many calls as the question needs.

        Answer the question directly, in a few short paragraphs or a list: the answer is shown in a narrow panel beside the diagram. Write every node you mention in backticks, by its name exactly as the tools give it (such as `Dependinator.Core.Parsing.ParserService`), so the user can click it to go to the node. Under nodes, list the nodes the answer is about, at most {MaxNodes}, the most important first. If the model cannot answer the question, say so, and what would.

        Under change: if your answer proposes a concrete change to the code (a split, a move, a rename, removing a dependency) that a coding AI could make from your answer, name the change in one imperative sentence, such as "Move loading and saving from ModelService to a new ModelStore", and make sure the answer says enough to carry it out. Otherwise leave it empty: an answer that only explains proposes no change.

        """;
}

// "Ask" (AskPanel): a question about the open model for an AI, which finds the answer with
// Dependinator's model tools and names the nodes it is about, as links in the panel. Asked through
// Claude with the user's API key (the host runs the tool-use loop, IClaudeService.
// AskWithModelToolsAsync), Copilot in VS Code (the LSP runs the loop and asks the model a turn at a
// time, IVsCodeModelService.AskWithModelToolsAsync), or Claude Code on this computer (Settings ›
// AI); all of them get the tools through the app's MCP server. The panel sits top-right with the analysis panels, so
// opening it closes them.
interface IAskModelService
{
    // Whether this host can ask an AI: the web host and VS Code, not the standalone browser app.
    bool IsAvailable { get; }

    // The prompt for the question, a follow-up on the earlier ones; empty when there is no model.
    string GetPrompt(string question, IReadOnlyList<AskExchange> earlier, AskContext context);

    // The prompt that asks an AI that can change the code to make the change the answer proposes
    // (AskChangePrompt); empty when it proposes none.
    // aboutNames names what the question was about as it was asked (the nodes may be gone since).
    string GetChangePrompt(ModelAnswer answer, AskContext context, string aboutNames);

    Task<Result<ModelAnswer>> AskAsync(
        string question,
        IReadOnlyList<AskExchange> earlier,
        AskContext context,
        AiSource route,
        Action<SuggestStep> onStep
    );

    void Toggle();

    // Shows the panel (if it is not shown), to ask about the selected nodes.
    void Open();

    // Selects the node and shows the panel with the question filled in, to ask about the node.
    Task AskAboutAsync(NodeId nodeId, string question = "");

    // A node to ask about and a question to fill in ("" for none), once: the panel takes them
    // when it is shown and no answer is awaited.
    AskRequest? TakeRequest();

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
    IModelQueries modelQueries,
    ISelectionService selectionService
) : IAskModelService
{
    public bool IsAvailable => aiService.IsAvailable;

    AskRequest? request;

    public string GetPrompt(string question, IReadOnlyList<AskExchange> earlier, AskContext context)
    {
        using var model = modelMgr.UseModel();
        return AskModelPrompt.Build(model, question, earlier, context);
    }

    public string GetChangePrompt(ModelAnswer answer, AskContext context, string aboutNames)
    {
        using var model = modelMgr.UseModel();
        return AskChangePrompt.Build(model, answer, context, aboutNames);
    }

    public async Task<Result<ModelAnswer>> AskAsync(
        string question,
        IReadOnlyList<AskExchange> earlier,
        AskContext context,
        AiSource route,
        Action<SuggestStep> onStep
    )
    {
        // The e2e tests exercise the panel without a key or Claude Code: two lookups, as an agent
        // would make them through the tools, and a canned answer.
        if (Dependinator.Core.Build.IsTestMode)
        {
            onStep(route.AskingStep());
            var lookups = new ScopeModelQueries(modelQueries);
            await lookups.GetModelAsync();
            await lookups.SearchNodesAsync(question);
            await Task.Delay(TestModeAnswerDelay);
            // A question about a split gets an answer that proposes a change.
            var change = question.Contains("split", StringComparison.OrdinalIgnoreCase) ? "Test mode: split it." : "";
            return WithLinks(
                TestModeAnswer(route, earlier.Count, context) with
                {
                    Question = question,
                    Change = change,
                }
            );
        }

        // AiService's own error for an empty prompt is about a review dialog.
        var prompt = GetPrompt(question, earlier, context);
        if (prompt == "")
            return new Error("There is no model to ask about yet: open or parse one first.");
        Task<string> GetPromptAsync() => Task.FromResult(prompt);

        if (route is AiSource.ClaudeCode)
        {
            // The setting may have been turned off since the panel offered the route.
            if (!await aiService.IsClaudeCodeEnabledAsync())
                return new Error("Claude Code is turned off. Turn it on in Settings › AI.");
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
            answered = await aiService.AskWithModelToolsAsync(route, GetPromptAsync, ModelAnswer.JsonSchema, onStep);
        }
        catch (Exception e) when (e.IsNotFatal())
        {
            return new Error($"Could not ask {route.Name()}: {e.Message}", e);
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
            if (FindLinkedNode(model, name) is { } node)
                links[name] = new NodeLink(node.Id, node.ShortName, node.LongName);
        }
        return answer with { Links = links };
    }

    // A node the answer names: by its name or title as the tools give them, or else by the end of
    // its title ("PromptParts.ReadSourcesAsync", "ReadSourcesAsync()"), as answers often write it,
    // when only one node (or one method's overloads) ends so.
    internal static Node? FindLinkedNode(IModel model, string name)
    {
        if (ModelQueries.FindNode(model, name) is Node node)
            return node;
        var tail = WithoutParameters(name.Trim());
        if (tail.Length < 3 || tail.Any(char.IsWhiteSpace))
            return null;
        var matches = model
            .Nodes.Values.Where(n => !n.IsRoot)
            .Select(n => (Node: n, Title: WithoutParameters(n.LongName)))
            .Where(n => n.Title == tail || n.Title.EndsWith("." + tail, StringComparison.Ordinal))
            .ToList();
        if (matches.Count == 1)
            return matches[0].Node;
        var isOverloads =
            matches.Count > 1
            && matches.All(m =>
                m.Node.Type.IsMember && m.Title == matches[0].Title && m.Node.Parent == matches[0].Node.Parent
            );
        return isOverloads ? matches.MinBy(m => m.Node.Name, StringComparer.Ordinal).Node : null;
    }

    static string WithoutParameters(string name) => name.IndexOf('(') is var at and >= 0 ? name[..at] : name;

    public void Toggle()
    {
        if (viewOptions.IsAskShown)
        {
            Close();
            return;
        }
        Open();
    }

    public async Task AskAboutAsync(NodeId nodeId, string question = "")
    {
        // Selecting a node that is the primary of several keeps the others, so start over. The
        // selection may refuse the node (one that fills the view, say): the panel then asks
        // about the requested node all the same.
        if (!selectionService.SelectedNodeIds.SequenceEqual([nodeId]))
        {
            selectionService.Unselect();
            await selectionService.Select(nodeId);
        }
        request = new AskRequest(nodeId, question);
        // The panel takes the request on the UI state change, which opening it raises.
        if (viewOptions.IsAskShown)
            applicationEvents.TriggerUIStateChanged();
        else
            Open();
    }

    public AskRequest? TakeRequest()
    {
        var taken = request;
        request = null;
        return taken;
    }

    public void Open()
    {
        if (viewOptions.IsAskShown)
            return;
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
    ModelAnswer TestModeAnswer(AiSource route, int earlierCount, AskContext context)
    {
        var followUp = earlierCount > 0 ? $" A follow-up, after {earlierCount} answered." : "";
        List<Node> top;
        using (var model = modelMgr.UseModel())
        {
            top = [.. model.Root.Children.Where(n => !NodeViewPolicy.IsAnnotation(n)).OrderBy(n => n.Name).Take(2)];
            var about = AskModelPrompt.ContextNodes(model, context);
            if (about.Count > 0)
            {
                var inside = context.IsBelow ? " with everything inside" : "";
                followUp += $" About {string.Join(", ", about.Select(n => n.ShortName))}{inside}.";
            }
        }
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
            ToolCalls = route is AiSource.Claude or AiSource.Copilot ? 2 : null,
            CostUsd = route is AiSource.Claude ? 0.0123m : null,
        };
    }

    static readonly TimeSpan TestModeAnswerDelay = TimeSpan.FromMilliseconds(800);
}

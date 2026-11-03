using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using static Dependinator.UI.Diagrams.PromptParts;

namespace Dependinator.UI.Diagrams;

// One stop on a codebase tour: the node as the AI named it, a short title and a few sentences on
// it. Id and LongName are the node Dependinator found for the name (CodebaseTour.Resolve); a stop
// whose name is no node is left out of the tour.
public record TourStop(string Name, string Title, string Text)
{
    public NodeId? Id { get; init; }

    public string LongName { get; init; } = "";
}

// A guided tour of the open model an AI picked (TourPrompt): a few key nodes in the order a
// newcomer should read them, each with a sentence or two, played one stop at a time over the
// diagram (TourCallout). Not saved with the model yet.
public record CodebaseTour(string Intro, IReadOnlyList<TourStop> Stops) : IAiAnswer
{
    public AiSource Source { get; init; } = AiSource.Claude;

    // The model the tour is about: a tour ends when another model opens.
    public string ModelPath { get; init; } = "";

    // How many stops the AI named that are not nodes, and were left out.
    public int LeftOutCount { get; init; }

    public string Verdict => Stops.Count == 1 ? "1 stop" : $"{Stops.Count} stops";

    // The fields of TourPrompt's answer format; structured output makes the JSON match it.
    public const string JsonSchema = """
        {
          "type": "object",
          "properties": {
            "intro": { "type": "string", "description": "1–2 sentences: what the codebase is and how the tour goes." },
            "stops": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "node": { "type": "string", "description": "The name of the node, as listed (the name in backticks)." },
                  "title": { "type": "string", "description": "A title of 2–6 words for the stop." },
                  "text": { "type": "string", "description": "2–3 sentences: what it does, why it matters, how it relates to the stops before it." }
                },
                "required": ["node", "title", "text"],
                "additionalProperties": false
              }
            }
          },
          "required": ["intro", "stops"],
          "additionalProperties": false
        }
        """;

    public static Result<CodebaseTour> Parse(string json, AiSource source = AiSource.Claude)
    {
        var parsed = Result.Catch(() => Json.Deserialize<Answer>(json) ?? throw new JsonException("No answer"));
        if (parsed is not Answer answer)
            return new Error($"{source.Name()}'s answer could not be read.", parsed.Error);
        List<TourStop> stops =
        [
            .. (answer.Stops ?? [])
                .Where(s => !string.IsNullOrWhiteSpace(s.Node))
                .Select(s => new TourStop(NodeName(s.Node!), (s.Title ?? "").Trim(), (s.Text ?? "").Trim())),
        ];
        return stops.Count == 0
            ? new Error($"{source.Name()} gave a tour without stops.")
            : new CodebaseTour((answer.Intro ?? "").Trim(), stops) { Source = source };
    }

    record Answer(string? Intro, List<AnswerStop>? Stops);

    record AnswerStop(
        [property: JsonPropertyName("node")] string? Node,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("text")] string? Text
    );

    // Labels of the answer format, markdown emphasis, list marks and headings aside.
    static readonly Regex LabelLine = new(
        @"^[\s>*_#\-\d.)]*(?<label>Intro|Stop|Title|Text)\s*\d*[*_]*\s*:[*_]*\s*(?<value>.*)$",
        RegexOptions.IgnoreCase
    );

    // An AI chat's answer to the copied prompt, pasted back: an "Intro:", then for each stop a
    // "Stop:" line with the node's name, a "Title:" and its "Text:", which runs until the next
    // label (a stop's other lines count as its text too).
    public static Result<CodebaseTour> ParseText(string text)
    {
        var intro = "";
        List<TourStop> stops = [];
        string? name = null;
        string title = "";
        StringBuilder body = new();
        string? field = null;

        void EndStop()
        {
            if (name is not null && name != "")
                stops.Add(new TourStop(name, title, body.ToString().Trim()));
            name = null;
            title = "";
            body.Clear();
        }

        foreach (var line in text.Replace("\r", "").Split('\n'))
        {
            if (LabelLine.Match(line) is { Success: true } labeled)
            {
                var value = labeled.Groups["value"].Value.Trim();
                field = labeled.Groups["label"].Value.ToLowerInvariant();
                switch (field)
                {
                    case "intro":
                        intro = Plain(value);
                        break;
                    case "stop":
                        EndStop();
                        name = NodeName(value);
                        break;
                    case "title":
                        title = Plain(value);
                        break;
                    default:
                        body.Append(value).Append(' ');
                        break;
                }
                continue;
            }
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (name is not null)
                body.Append(line.Trim()).Append(' ');
            else if (field == "intro")
                intro = $"{intro} {line.Trim()}".Trim();
        }
        EndStop();

        return stops.Count == 0
            ? new Error("The answer has no \"Stop:\" lines, so it could not be read.")
            : new CodebaseTour(intro, stops) { Source = AiSource.Chat };
    }

    // A node name as an answer writes it: in backticks, quotes or bold, maybe with a title after a
    // dash ("`Dependinator.Core` – the core").
    static string NodeName(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith('`') && trimmed.IndexOf('`', 1) is var end and > 1)
            return trimmed[1..end].Trim();
        var cut = trimmed.IndexOfAny(['—', '–']);
        if (cut < 0)
            cut = trimmed.IndexOf(" - ", StringComparison.Ordinal);
        if (cut > 0)
            trimmed = trimmed[..cut];
        return trimmed.Trim().Trim('`', '*', '_', '"', '\'', '.', ',', ';', ':').Trim();
    }

    static string Plain(string text) => text.Trim().Trim('*', '_').Trim();

    // The stops whose names are nodes in the model, each once (the first stop counts), with the
    // node Dependinator found for it; a stop without a title gets the node's short name.
    internal static Result<CodebaseTour> Resolve(CodebaseTour tour, IModel model)
    {
        HashSet<NodeId> seen = [];
        List<TourStop> stops = [];
        foreach (var stop in tour.Stops)
        {
            if (
                AskModelService.FindLinkedNode(model, stop.Name) is not { } node
                || node.IsRoot
                || NodeViewPolicy.IsAnnotation(node)
                || !seen.Add(node.Id)
            )
                continue;
            stops.Add(
                stop with
                {
                    Id = node.Id,
                    LongName = node.LongName,
                    Title = stop.Title != "" ? stop.Title : node.ShortName,
                }
            );
        }
        if (stops.Count == 0)
            return new Error($"None of the stops {tour.Source.Name()} named is a node in the model.");
        return tour with { Stops = stops, ModelPath = model.Path, LeftOutCount = tour.Stops.Count - stops.Count };
    }
}

// The prompt that asks an AI for a guided tour of the model: what Dependinator shows, the model's
// structure and the dependencies between its containers (SuggestRulesPrompt.Outline), and the
// task, in a format a chat answer can be read back from.
static class TourPrompt
{
    internal const int MinStops = 5;
    internal const int MaxStops = 8;

    public static string Build(IModel model)
    {
        var outline = SuggestRulesPrompt.Outline(model, withExternals: false);
        if (outline == "")
            return "";

        var text = new StringBuilder();
        text.AppendLine("# A guided tour of a codebase").AppendLine();
        text.AppendLine(
            "Dependinator shows a codebase as a dependency model: containers (projects, namespaces, modules), the "
                + "types in them, and the links between them. It can walk a newcomer through the codebase, one "
                + "stop at a time: it zooms the diagram to each stop's node and shows the stop's text beside it."
        );
        text.AppendLine().Append(outline);
        text.AppendLine().Append(TaskSection);
        return text.ToString();
    }

    static readonly string TaskSection = $"""
        ## Your task

        Plan a tour of {MinStops} to {MaxStops} stops for a developer who is new to this codebase, in the order they should read it: usually where it starts (the host or entry point), then the main flow through its core parts, then what supports them. Pick the nodes that explain the architecture best, not the biggest ones. Each stop is a node, named exactly as listed (the name in backticks); with Dependinator's tools you may also pick a type you looked up, named as the tools name it. For each stop write a title of 2–6 words and 2–3 sentences: what it does, why it matters, and how it relates to the stops before it. Start with an intro of 1–2 sentences on what the codebase is. If you have Dependinator's tools, look up the dependencies of the candidates before you choose.

        Answer in this format:

        Intro: 1–2 sentences.

        Stop: <name>
        Title: 2–6 words
        Text: 2–3 sentences.

        """;
}

// "Tour This Codebase": an AI (Claude, Claude Code or Copilot with Dependinator's model tools, or
// the user's AI chat) picks a few key nodes in reading order (TourPanel), and the tour plays one
// stop at a time (TourCallout): each stop zooms to and selects its node, with Back and Next.
interface ITourService
{
    // Whether this host can ask an AI: the web host and VS Code, not the standalone browser app.
    bool IsAvailable { get; }

    // The tour being played, and the index of the stop shown; null when no tour plays.
    CodebaseTour? Playing { get; }
    int StopIndex { get; }

    // Raised when a tour starts, moves to another stop or ends.
    event Action? Changed;

    // The prompt for any AI; empty when there is no model.
    string GetPrompt();

    Task<Result<CodebaseTour>> AskAsync(AiSource route, Action<SuggestStep> onStep);

    // Reads an AI chat's answer, pasted back, and finds its stops' nodes.
    Result<CodebaseTour> ParsePasted(string text);

    // Plays the tour from the stop, zooming to its node; the panel hides while it plays.
    Task StartAsync(CodebaseTour tour, int stopIndex = 0);

    Task NextAsync();
    Task BackAsync();

    // Stops playing; the panel shows again if it was open.
    void End();

    // Opens the panel, closing the others in its corner.
    void Open();

    void Close();
}

[Scoped]
class TourService : ITourService
{
    readonly IModelMgr modelMgr;
    readonly IModelService modelService;
    readonly IAiService aiService;
    readonly IViewOptions viewOptions;
    readonly IPathFinderService pathFinderService;
    readonly INavigationService navigationService;
    readonly IApplicationEvents applicationEvents;

    public TourService(
        IModelMgr modelMgr,
        IModelService modelService,
        IAiService aiService,
        IViewOptions viewOptions,
        IPathFinderService pathFinderService,
        INavigationService navigationService,
        IApplicationEvents applicationEvents
    )
    {
        this.modelMgr = modelMgr;
        this.modelService = modelService;
        this.aiService = aiService;
        this.viewOptions = viewOptions;
        this.pathFinderService = pathFinderService;
        this.navigationService = navigationService;
        this.applicationEvents = applicationEvents;
        applicationEvents.ModelChanged += OnModelChanged;
    }

    public bool IsAvailable => aiService.IsAvailable;

    public CodebaseTour? Playing { get; private set; }

    public int StopIndex { get; private set; }

    public event Action? Changed;

    public string GetPrompt()
    {
        using var model = modelMgr.UseModel();
        return TourPrompt.Build(model);
    }

    public async Task<Result<CodebaseTour>> AskAsync(AiSource route, Action<SuggestStep> onStep)
    {
        // The e2e tests exercise the panel without a key or network.
        if (Dependinator.Core.Build.IsTestMode)
        {
            onStep(route.AskingStep());
            await Task.Delay(TestModeAnswerDelay);
            return Resolve(TestModeAnswer() with { Source = route });
        }

        var json = await aiService.AskAsync(
            route,
            () => Task.FromResult(GetPrompt()),
            CodebaseTour.JsonSchema,
            onStep,
            withModelTools: true
        );
        if (json is not string text)
            return json.Error;
        var parsed = CodebaseTour.Parse(text, route);
        return parsed is CodebaseTour tour ? Resolve(tour) : parsed.Error;
    }

    public Result<CodebaseTour> ParsePasted(string text)
    {
        var parsed = CodebaseTour.ParseText(text);
        return parsed is CodebaseTour tour ? Resolve(tour) : parsed.Error;
    }

    Result<CodebaseTour> Resolve(CodebaseTour tour)
    {
        using var model = modelMgr.UseModel();
        return CodebaseTour.Resolve(tour, model);
    }

    public async Task StartAsync(CodebaseTour tour, int stopIndex = 0)
    {
        if (tour.Stops.Count == 0)
            return;
        Playing = tour;
        StopIndex = Math.Clamp(stopIndex, 0, tour.Stops.Count - 1);
        await ShowStopAsync();
    }

    public Task NextAsync() => MoveAsync(+1);

    public Task BackAsync() => MoveAsync(-1);

    async Task MoveAsync(int step)
    {
        if (Playing is not { } tour)
            return;
        var index = StopIndex + step;
        if (index < 0 || index >= tour.Stops.Count)
            return;
        StopIndex = index;
        await ShowStopAsync();
    }

    // Tells the callout first, so the text changes as the diagram moves to the node.
    async Task ShowStopAsync()
    {
        if (Playing is not { } tour)
            return;
        if (tour.ModelPath != modelMgr.WithModel(m => m.Path))
        {
            End();
            return;
        }
        Changed?.Invoke();
        applicationEvents.TriggerUIStateChanged();
        if (tour.Stops[StopIndex].Id is { } nodeId)
            await navigationService.ShowNodeAsync(nodeId);
    }

    // Another model opened: the tour ends. A re-parse of the same model removed a stop's node:
    // that stop is left out, and the tour stays on the stop it showed where it can.
    internal void OnModelChanged()
    {
        if (Playing is not { } tour)
            return;
        List<TourStop> stops;
        using (var model = modelMgr.UseModel())
        {
            if (model.Path == "")
                return; // Loading, maybe the same model again.
            if (model.Path != tour.ModelPath)
            {
                End();
                return;
            }
            stops = [.. tour.Stops.Where(s => s.Id is { } id && model.Nodes.ContainsKey(id))];
        }
        if (stops.Count == tour.Stops.Count)
            return;
        if (stops.Count == 0)
        {
            End();
            return;
        }
        var shown = tour.Stops[StopIndex];
        var index = stops.IndexOf(shown);
        StopIndex = index >= 0 ? index : Math.Min(stops.Count - 1, tour.Stops.Take(StopIndex).Count(stops.Contains));
        Playing = tour with { Stops = stops };
        Changed?.Invoke();
    }

    public void End()
    {
        if (Playing is null)
            return;
        Playing = null;
        StopIndex = 0;
        Changed?.Invoke();
        applicationEvents.TriggerUIStateChanged();
    }

    public void Open()
    {
        if (viewOptions.IsTourShown)
            return;
        // The panels share the top-right corner; the analysis panels' highlighted lines are baked
        // into the tiles.
        if (viewOptions.IsCyclesShown || viewOptions.IsRulesShown)
        {
            viewOptions.SetCyclesShown(false);
            viewOptions.SetRulesShown(false);
            modelService.ClearCache();
        }
        viewOptions.SetHiddenNodesShown(false);
        pathFinderService.Close();
        viewOptions.SetTourShown(true);
        applicationEvents.TriggerUIStateChanged();
    }

    public void Close()
    {
        End();
        viewOptions.SetTourShown(false);
        applicationEvents.TriggerUIStateChanged();
    }

    // The first three containers of the codebase itself (not its external references, which the
    // prompt leaves out too), and one that is not in the model (left out).
    CodebaseTour TestModeAnswer()
    {
        List<Node> top;
        using (var model = modelMgr.UseModel())
            top =
            [
                .. model
                    .Root.Children.Where(n =>
                        !NodeViewPolicy.IsAnnotation(n) && n.Type != Dependinator.Core.Parsing.NodeType.Externals
                    )
                    .OrderBy(n => n.Name, StringComparer.Ordinal)
                    .SelectMany(n => n.DescendantsAndSelfPreOrder())
                    .Where(n => !n.Type.IsMember && !NodeViewPolicy.IsAnnotation(n))
                    .Take(3),
            ];
        List<TourStop> stops =
        [
            .. top.Select(
                (n, i) => new TourStop(n.Name, $"Stop {i + 1}", $"Test mode: Claude was not asked about `{n.Name}`.")
            ),
            new TourStop("No.Such.Node", "Not in the model", "Test mode: a node that is not in the model."),
        ];
        return new CodebaseTour("Test mode: Claude was not asked for a tour.", stops);
    }

    static readonly TimeSpan TestModeAnswerDelay = TimeSpan.FromMilliseconds(800);
}

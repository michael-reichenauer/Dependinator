using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using MudBlazor;
using static Dependinator.UI.Diagrams.PromptParts;

namespace Dependinator.UI.Diagrams;

// How many namespaces and types a run would comment, and in how many files.
public record CommentsScope(int Targets, int Files);

// "Improve Comments": Claude Code adds or rewrites the comments of the chosen nodes' namespaces and
// types (or of everything inside them), which the diagram shows as their descriptions
// (ImproveCommentsDialog, CommentsPrompt). It runs on the code as it is, on the current branch,
// like a fix. Where the experimental Claude Code route is off, the prompt can be copied into any
// AI that can change the code. Reached from the node menu (one or several selected nodes) and the
// About dialog.
interface IImproveCommentsService
{
    Task ShowAsync(IReadOnlyList<NodeId> nodeIds);

    // Whether the nodes, or anything inside them, have a comment the parser reads.
    bool CanImprove(IReadOnlyList<NodeId> nodeIds);

    CommentsScope GetScope(IReadOnlyList<NodeId> nodeIds, bool isBelow);

    // The prompt for Claude Code; empty when nothing in the scope can have a comment.
    string GetPrompt(IReadOnlyList<NodeId> nodeIds, bool isBelow);
}

[Scoped]
class ImproveCommentsService(IModelMgr modelMgr, IAiService aiService, IDialogService dialogService)
    : IImproveCommentsService
{
    public async Task ShowAsync(IReadOnlyList<NodeId> nodeIds)
    {
        string name;
        bool isSeveral;
        bool hasChildren;
        using (var model = modelMgr.UseModel())
        {
            var nodes = Nodes(model, nodeIds);
            if (nodes.Count == 0)
                return;
            isSeveral = nodes.Count > 1;
            name = isSeveral ? $"the {nodes.Count} selected nodes" : nodes[0].LongName;
            // Members are children too, but have no comment to improve here.
            hasChildren = nodes.Any(n => n.DescendantsAndSelfPreOrder().Skip(1).Any(CommentsPrompt.IsCommentable));
        }

        var parameters = new DialogParameters<ImproveCommentsDialog>
        {
            { d => d.Name, name },
            { d => d.IsSeveral, isSeveral },
            { d => d.HasChildren, hasChildren },
            { d => d.CanRun, await aiService.CanFixWithClaudeCodeAsync() },
            { d => d.GetScope, isBelow => GetScope(nodeIds, isBelow) },
            { d => d.GetPrompt, isBelow => GetPrompt(nodeIds, isBelow) },
        };
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
        };
        await dialogService.ShowAsync<ImproveCommentsDialog>(null, parameters, options);
    }

    public bool CanImprove(IReadOnlyList<NodeId> nodeIds)
    {
        using var model = modelMgr.UseModel();
        return Nodes(model, nodeIds).Any(n => n.DescendantsAndSelfPreOrder().Any(CommentsPrompt.IsCommentable));
    }

    public CommentsScope GetScope(IReadOnlyList<NodeId> nodeIds, bool isBelow)
    {
        using var model = modelMgr.UseModel();
        var targets = CommentsPrompt.Targets(Nodes(model, nodeIds), isBelow);
        return new(targets.Count, targets.Select(t => t.FileSpan!.Path).Distinct().Count());
    }

    public string GetPrompt(IReadOnlyList<NodeId> nodeIds, bool isBelow)
    {
        using var model = modelMgr.UseModel();
        return CommentsPrompt.Build(model, CommentsPrompt.Targets(Nodes(model, nodeIds), isBelow));
    }

    static List<Node> Nodes(IModel model, IReadOnlyList<NodeId> nodeIds) =>
        [
            .. nodeIds
                .Select(id => model.Nodes.TryGetValue(id, out var node) ? node : null)
                .OfType<Node>()
                .Where(n => !n.IsRoot && !NodeViewPolicy.IsAnnotation(n)),
        ];
}

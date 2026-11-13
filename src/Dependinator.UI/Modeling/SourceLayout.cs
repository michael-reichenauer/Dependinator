using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling;

// Brings the user's work on a source's own model into a workspace model that gets that source:
// where the nodes are and how they are drawn (aliases, colors, icons, hidden or dimmed), the notes,
// zones and other nodes the user drew in it, and the lines the user routed or labelled.
// Only what is inside the source's root is taken: the root's place among the workspace's sources,
// the shared Externals and a project another source shows are the workspace model's own.
static class SourceLayout
{
    // sourceModel: the source's own saved model; rootName: the source's root in the workspace
    // model. A node is named as in its own model, but for the names under a root that the
    // workspace renamed on a name clash (WorkspaceItems.Renamed); a C# project's nodes are not
    // named by their solution, so what is in the root goes by the tree, never by the name.
    public static void Apply(IModel model, IStructureService structure, ModelDto sourceModel, string rootName)
    {
        if (SourceRootName(sourceModel, rootName) is not { } sourceRootName)
            return;
        if (!model.Nodes.TryGetValue(NodeId.FromName(rootName), out var root))
            return;

        var names = new NameMap(sourceRootName, rootName);
        var inRoot = root.DescendantsAndSelfPreOrder().Select(n => n.Name).ToHashSet(StringComparer.Ordinal);
        var sourceNodes = InRoot(sourceModel.Nodes, sourceRootName);

        foreach (var dto in sourceNodes)
        {
            var name = names.ToWorkspace(dto.Name);
            if (model.Nodes.TryGetValue(NodeId.FromName(name), out var node))
            {
                if (inRoot.Contains(name))
                    node.SetFromDto(WithLayout(node.ToDto(), dto, isRoot: node == root));
            }
            else if (dto.IsManual && names.ToWorkspace(dto.ParentName) is var parentName && inRoot.Contains(parentName))
            {
                structure.SetNodeDto(model, dto with { Name = name, ParentName = parentName });
                inRoot.Add(name);
            }
        }

        var sourceNames = sourceNodes.Select(n => n.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var dto in sourceModel.Links.Where(l => l.IsManual))
        {
            if (!sourceNames.Contains(dto.SourceName) || !sourceNames.Contains(dto.TargetName))
                continue;
            var source = names.ToWorkspace(dto.SourceName);
            var target = names.ToWorkspace(dto.TargetName);
            if (inRoot.Contains(source) && inRoot.Contains(target))
                structure.SetLinkDto(model, dto with { SourceName = source, TargetName = target });
        }

        ApplyLines(model, structure, sourceModel, names, inRoot);

        var nodes = root.DescendantsAndSelfPreOrder().ToList();
        foreach (var node in nodes)
        {
            MemberCard.RestoreLayoutState(node);
            // A node that is new since the source's model was saved has no place yet
            if (node.Children.Any(child => child.Boundary == Rect.None))
                node.IsChildrenLayoutRequired = true;
        }
        ModelService.RequireChildrenLayoutIfOlder(nodes, sourceModel.LayoutVersion);
        model.BumpVisibilityVersion();
        model.BumpStructureVersion();
    }

    // The root of the source in its own model: a node of the top level, named as in the
    // workspace model, or the only one of the source's kind there when the workspace renamed it.
    static string? SourceRootName(ModelDto sourceModel, string rootName)
    {
        var roots = sourceModel
            .Nodes.Where(n => n.ParentName == "" && n.Type == nameof(Parsing.NodeType.Solution))
            .Select(n => n.Name)
            .ToList();
        return roots.Contains(rootName) ? rootName
            : roots.Count == 1 ? roots[0]
            : null;
    }

    // The nodes of the source's own model that are in its root, parents before children.
    static IReadOnlyList<NodeDto> InRoot(IReadOnlyList<NodeDto> nodes, string rootName)
    {
        var children = nodes.ToLookup(n => n.ParentName, StringComparer.Ordinal);
        List<NodeDto> inRoot = [];
        var pending = new Queue<NodeDto>(nodes.Where(n => n.Name == rootName));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (pending.TryDequeue(out var node))
        {
            if (!seen.Add(node.Name))
                continue;
            inRoot.Add(node);
            foreach (var child in children[node.Name])
                pending.Enqueue(child);
        }
        return inRoot;
    }

    // The workspace node as parsed, with what the user set on it in the source's own model. The
    // root keeps its place among the sources and the visibility it inherits there.
    static NodeDto WithLayout(NodeDto node, NodeDto source, bool isRoot) =>
        node with
        {
            Boundary = isRoot ? node.Boundary : source.Boundary,
            Offset = source.Offset,
            Zoom = source.Zoom,
            Color = source.Color ?? node.Color,
            CustomColor = source.CustomColor,
            Alias = source.Alias,
            IconName = source.IconName,
            IconColor = source.IconColor,
            Visibility = source.Visibility,
            IsUserSetHidden = source.IsUserSetHidden,
            ParentVisibility = isRoot ? node.ParentVisibility : source.ParentVisibility,
            IsParentSetHidden = isRoot ? node.IsParentSetHidden : source.IsParentSetHidden,
            IsChildrenLayoutCustomized = source.IsChildrenLayoutCustomized,
            IsPrivateMembersShown = source.IsPrivateMembersShown,
            IsChildrenLayoutRequired = source.IsChildrenLayoutRequired,
        };

    // The lines inside the source's root that the user routed, described or labelled. Line ids
    // are hashes of the end names, so each line of the workspace model is looked up by the names
    // its ends have in the source's own model. A label that waits for its line (a split line,
    // shown only once rendered) is kept by its id, which holds only when no name was changed.
    static void ApplyLines(
        IModel model,
        IStructureService structure,
        ModelDto sourceModel,
        NameMap names,
        IReadOnlySet<string> inRoot
    )
    {
        var lines = sourceModel.Lines.DistinctBy(l => l.LineId).ToDictionary(l => l.LineId);
        var applied = new HashSet<string>();
        foreach (var line in model.Lines.Values.ToList())
        {
            if (!inRoot.Contains(line.Source.Name) || !inRoot.Contains(line.Target.Name))
                continue;

            var source = names.ToSource(line.Source.Name);
            var target = names.ToSource(line.Target.Name);
            var sourceId =
                line.IsDirect ? LineId.FromDirect(source, target)
                : line.IsInheritance ? LineId.FromInheritance(source, target)
                : LineId.From(source, target);
            if (!lines.TryGetValue(sourceId.Value, out var dto))
                continue;

            applied.Add(dto.LineId);
            structure.SetLineLayoutDto(
                model,
                dto with
                {
                    LineId = line.Id.Value,
                    Description = null, // Parsed, so taken from this parse only
                    Source = dto.Source is null ? null : line.Source.Name,
                    Target = dto.Target is null ? null : line.Target.Name,
                }
            );
        }

        if (!names.IsSame)
            return;
        foreach (var dto in sourceModel.Lines.Where(l => !applied.Contains(l.LineId)))
        {
            if (dto.Label is null && dto.LabelDescription is null)
                continue;
            if (model.Lines.ContainsKey(LineId.FromId(dto.LineId)))
                continue; // A line of the workspace model that is not in the root
            if (inRoot.Contains(dto.Source ?? "") && inRoot.Contains(dto.Target ?? ""))
                structure.SetLineLayoutDto(
                    model,
                    dto with
                    {
                        SegmentPoints = [],
                        IsSegmentsUserSet = false,
                        Description = null,
                    }
                );
        }
    }

    // The names under a root that the workspace renamed: the root and the names it prefixes.
    sealed record NameMap(string SourceRootName, string RootName)
    {
        public bool IsSame => SourceRootName == RootName;

        public string ToWorkspace(string name) => Renamed(name, SourceRootName, RootName);

        public string ToSource(string name) => Renamed(name, RootName, SourceRootName);

        static string Renamed(string name, string from, string to) =>
            name == from ? to
            : name.StartsWith(from + ".", StringComparison.Ordinal) ? to + name[from.Length..]
            : name;
    }
}

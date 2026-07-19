using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling;

interface IStructureService
{
    void AddOrUpdateNode(IModel model, Parsing.Node parsedNode);
    void AddOrUpdateLink(IModel model, Parsing.Link parsedLink);
    Models.Link? AddManualLink(IModel model, string sourceName, string targetName);
    void RenameNode(IModel model, string fromName, string toName);
    void SetLineDescription(IModel model, Parsing.LineDescription lineDescription);
    void ClearNotUpdated(IModel model);
    void SetNodeDto(IModel model, NodeDto nodeDto);
    void SetLinkDto(IModel model, LinkDto linkDto);
    void SetLineLayoutDto(IModel model, LineDto lineLayoutDto);
}

[Transient]
class StructureService(ILineService linesService) : IStructureService
{
    static readonly string ExternalsNodeName = "$Externals";
    static readonly string ExternalsDescription = Parsing.NodeDescriptions.Externals;

    public void AddOrUpdateNode(IModel model, Parsing.Node parsedNode)
    {
        if (!model.Nodes.TryGetValue(NodeId.FromName(parsedNode.Name), out var node))
        { // New node, add it to the model and parent
            var parent = GetOrCreateParent(model, parsedNode);

            node = new Models.Node(parsedNode.Name, parent);

            node.Update(parsedNode);
            node.UpdateStamp = model.UpdateStamp;

            model.TryAddNode(node);
            parent.AddChild(node);

            // A node added after its parent (or an ancestor) was hidden must inherit the hidden
            // state; otherwise its links make aggregated lines to/inside the hidden subtree
            // render as visible even though the subtree stays hidden.
            if (parent.IsHidden)
                node.SetHidden(true, isUserSet: false);

            return;
        }

        node.Update(parsedNode);
        node.UpdateStamp = model.UpdateStamp;

        var parentName = parsedNode.Properties.Parent;

        if (parentName is not null && node.Parent.Name != parentName)
        { // The node has changed parent, remove it from the old parent and add it to the new parent
            MoveNodeToParent(model, node, parentName);
        }
    }

    public void AddOrUpdateLink(IModel model, Parsing.Link parsedLink)
    {
        var linkId = new LinkId(parsedLink.Source, parsedLink.Target);
        var isParsedInheritance = parsedLink.Properties.IsInheritance == true;

        if (model.Links.TryGetValue(linkId, out var link))
        {
            // Within one parse (same stamp) any inheritance link wins over usage links (parse
            // order is not guaranteed); a new parse (new stamp) resets, so a link downgrades
            // when the code no longer inherits but still references the target.
            var isFirstInParse = link.UpdateStamp != model.UpdateStamp;
            var isInheritance = isParsedInheritance || (!isFirstInParse && link.IsInheritance);

            link.UpdateStamp = model.UpdateStamp;
            link.Source.UpdateStamp = model.UpdateStamp;
            link.Target.UpdateStamp = model.UpdateStamp;

            if (link.IsInheritance != isInheritance)
            { // Kind changed, rebuild the link's lines since inheritance links use separate lines
                model.RemoveLink(link);
                link.IsInheritance = isInheritance;
                AddLink(model, link);
            }
            return;
        }

        EnsureSourceAndTargetExists(model, parsedLink.Source, parsedLink.Target);

        var source = model.Nodes[NodeId.FromName(parsedLink.Source)];
        var target = model.Nodes[NodeId.FromName(parsedLink.Target)];
        if (parsedLink.Properties.TargetType is { } targetType && targetType is not NodeType.None)
            SetTargetTypeFromLink(target, targetType);

        link = new Models.Link(source, target);
        link.IsInheritance = isParsedInheritance;
        link.UpdateStamp = model.UpdateStamp;

        AddLink(model, link);
    }

    // Creates a user-drawn link (and its visual line) between two existing nodes. Returns null if
    // an endpoint is missing or the link already exists (so redo is idempotent).
    public Models.Link? AddManualLink(IModel model, string sourceName, string targetName)
    {
        var linkId = new LinkId(sourceName, targetName);
        if (model.Links.ContainsKey(linkId))
            return null;

        if (!model.Nodes.TryGetValue(NodeId.FromName(sourceName), out var source))
            return null;
        if (!model.Nodes.TryGetValue(NodeId.FromName(targetName), out var target))
            return null;

        var link = new Models.Link(source, target) { IsManual = true, UpdateStamp = model.UpdateStamp };

        AddLink(model, link);
        return link;
    }

    // Renames a node and its whole subtree by rebuilding each node under a new parent-qualified
    // name (the name is the node's identity): the renamed node takes toName and every descendant is
    // re-prefixed accordingly. All links touching any node in the subtree are recreated with the
    // remapped endpoint names (endpoints outside the subtree are unchanged). No-op if the source is
    // missing or the target name is already taken. Symmetric, so a command can revert by renaming
    // back.
    public void RenameNode(IModel model, string fromName, string toName)
    {
        if (fromName == toName)
            return;
        if (!model.Nodes.TryGetValue(NodeId.FromName(fromName), out var fromNode))
            return;
        if (model.Nodes.ContainsKey(NodeId.FromName(toName)))
            return;

        var rootParent = fromNode.Parent;
        var subtree = fromNode.DescendantsAndSelfPreOrder().ToList();

        // Plan each node's new name (parents before children): the root becomes toName; each
        // descendant keeps its short (last) name segment under its parent's new name. Capture the
        // DTOs and parent links now, before any mutation.
        var nameMap = new Dictionary<string, string>();
        var plan = new List<(string NewName, string? NewParentName, NodeDto Dto)>();
        foreach (var node in subtree)
        {
            var newName = node == fromNode ? toName : $"{nameMap[node.Parent.Name]}.{LastNameSegment(node.Name)}";
            var newParentName = node == fromNode ? null : nameMap[node.Parent.Name];
            nameMap[node.Name] = newName;
            plan.Add((newName, newParentName, node.ToDto()));
        }

        // Collect links touching any subtree node, remapping endpoints that fall inside the subtree.
        var seen = new HashSet<LinkId>();
        var remappedLinks = new List<(string Source, string Target)>();
        foreach (var node in subtree)
        {
            foreach (var link in node.SourceLinks.Concat(node.TargetLinks))
            {
                if (!seen.Add(link.Id))
                    continue;
                var source = nameMap.GetValueOrDefault(link.Source.Name, link.Source.Name);
                var target = nameMap.GetValueOrDefault(link.Target.Name, link.Target.Name);
                remappedLinks.Add((source, target));
            }
        }

        // Remove the old links and nodes, then rebuild the subtree under the new names and re-add the
        // remapped links (and their lines).
        var oldLinks = subtree.SelectMany(n => n.SourceLinks.Concat(n.TargetLinks)).Distinct().ToList();
        oldLinks.ForEach(model.RemoveLink);
        foreach (var node in fromNode.DescendantsAndSelfPostOrder().ToList())
            model.RemoveNode(node);

        foreach (var (newName, newParentName, dto) in plan)
        {
            var parent = newParentName is null ? rootParent : model.Nodes[NodeId.FromName(newParentName)];
            var newNode = new Models.Node(newName, parent);
            newNode.SetFromDto(dto);
            newNode.UpdateStamp = model.UpdateStamp;
            model.TryAddNode(newNode);
            parent.AddChild(newNode);
        }

        foreach (var (source, target) in remappedLinks)
            AddManualLink(model, source, target);
    }

    static string LastNameSegment(string name)
    {
        var index = name.LastIndexOf('.');
        return index < 0 ? name : name[(index + 1)..];
    }

    public void SetNodeDto(IModel model, NodeDto nodeDto)
    {
        if (nodeDto.Name == "") // Root node already exists
            return;

        // Persisted FileSpan paths are relative to the model folder; the in-memory model
        // uses absolute paths (matched against editor file paths for code navigation).
        nodeDto = FileSpanPaths.ToAbsolute(nodeDto, model.Path);

        var parent = GetOrCreateParent(model, nodeDto);

        var node = new Models.Node(nodeDto.Name, parent);
        node.SetFromDto(nodeDto);
        node.UpdateStamp = model.UpdateStamp;

        model.TryAddNode(node);
        parent.AddChild(node);
    }

    public void SetLinkDto(IModel model, LinkDto linkDto)
    {
        EnsureSourceAndTargetExists(model, linkDto.SourceName, linkDto.TargetName);

        var source = model.Nodes[NodeId.FromName(linkDto.SourceName)];
        var target = model.Nodes[NodeId.FromName(linkDto.TargetName)];
        var targetType = Enums.To<NodeType>(linkDto.TargetType, NodeType.None);
        if (targetType is not NodeType.None)
            SetTargetTypeFromLink(target, targetType);

        var link = new Models.Link(source, target);
        link.UpdateStamp = model.UpdateStamp;
        link.IsManual = linkDto.IsManual;
        link.IsInheritance = linkDto.IsInheritance;

        AddLink(model, link);
    }

    // A link only carries the generic NodeType.Type for type targets, so don't let it overwrite an
    // already-known specific type kind (e.g. InterfaceType) the target node was parsed with.
    static void SetTargetTypeFromLink(Models.Node target, NodeType targetType)
    {
        if (targetType == NodeType.Type && target.Type.IsType)
            return;

        target.Type = targetType;
    }

    // Sets a parsed line description on the line whose endpoints exactly match the description's
    // source and resolved target. The target name may be relative; it is resolved like C# name
    // lookup by prefixing the source node's ancestors. If no matching line exists, the
    // description is silently unused; nodes, links, or lines are never created.
    public void SetLineDescription(IModel model, Parsing.LineDescription lineDescription)
    {
        if (!model.Nodes.TryGetValue(NodeId.FromName(lineDescription.Source), out var source))
            return;

        foreach (var targetName in CandidateTargetNames(source, lineDescription.Target))
        {
            if (
                model.Lines.TryGetValue(LineId.From(lineDescription.Source, targetName), out var line)
                || model.Lines.TryGetValue(LineId.FromInheritance(lineDescription.Source, targetName), out line)
            )
            {
                line.SetDescription(lineDescription.Text, model.UpdateStamp);
                return;
            }
        }
    }

    static IEnumerable<string> CandidateTargetNames(Models.Node source, string target)
    {
        yield return target;

        foreach (var ancestor in source.Ancestors())
        {
            if (ancestor.IsRoot)
                yield break;
            yield return $"{ancestor.Name}.{target}";
        }
    }

    public void SetLineLayoutDto(IModel model, LineDto lineLayoutDto)
    {
        if (!model.Lines.TryGetValue(LineId.FromId(lineLayoutDto.LineId), out var line))
            return;

        line.SetSegmentPoints(lineLayoutDto.SegmentPoints);
        line.IsSegmentsUserSet = lineLayoutDto.IsSegmentsUserSet ?? lineLayoutDto.SegmentPoints.Count > 0;
        if (lineLayoutDto.Description is not null)
            line.SetDescription(lineLayoutDto.Description, model.UpdateStamp);
    }

    public void ClearNotUpdated(IModel model)
    {
        // Manually added nodes/links are not produced by parsing, so they are always "stale" by
        // stamp; exempt them so a re-parse doesn't delete the user's design work.
        var links = model.Links.Values.Where(l => !l.IsManual && l.UpdateStamp != model.UpdateStamp).ToList();
        Log.Info($"Remove {links.Count} links");
        foreach (var link in links)
            model.RemoveLink(link);

        var nodes = model
            .Nodes.Values.Where(n => !n.IsManual && n.UpdateStamp != model.UpdateStamp && n.Children.Count == 0)
            .ToList();
        Log.Info($"Remove {nodes.Count} nodes");
        foreach (var node in nodes)
            RemoveNode(model, node);

        // A manual link may point at a parsed node that was just removed (deleted from code);
        // drop such dangling manual links so no line references a node no longer in the model.
        var danglingManualLinks = model
            .Links.Values.Where(l =>
                l.IsManual && (!model.Nodes.ContainsKey(l.Source.Id) || !model.Nodes.ContainsKey(l.Target.Id))
            )
            .ToList();
        foreach (var link in danglingManualLinks)
            model.RemoveLink(link);

        // Clear line descriptions whose source comments were removed since the last parse
        var staleDescriptionLines = model
            .Lines.Values.Where(l => l.Description is not null && l.DescriptionUpdateStamp != model.UpdateStamp)
            .ToList();
        foreach (var line in staleDescriptionLines)
            line.ClearDescription();
    }

    void MoveNodeToParent(IModel model, Models.Node node, string parentName)
    {
        // Link lines need to be re-adjusted, so first remove all links and lines
        var lines = node.SourceLines.Concat(node.TargetLines).Concat(node.DirectLines).ToList();
        var links = lines.SelectMany(line => line.Links).ToList();

        links.ForEach(model.RemoveLink);

        node.Parent.RemoveChild(node);
        var parent = GetOrCreateNode(model, parentName);
        parent.AddChild(node);

        // Sync inherited hidden state with the new parent: hide when moved into a hidden
        // subtree, and clear a stale parent-set hidden flag when moved out of one.
        node.SetHidden(parent.IsHidden, isUserSet: false);

        // Re-add link and lines again
        links.ForEach(l => AddLink(model, l));
    }

    void AddLink(IModel model, Models.Link link)
    {
        model.TryAddLink(link);
        link.Target.AddTargetLink(link);
        if (link.Source.AddSourceLink(link))
        {
            linesService.AddLinesFromSourceToTarget(model, link);
        }
    }

    Models.Node GetOrCreateNode(IModel model, string name)
    {
        var nodeId = NodeId.FromName(name);
        if (!model.Nodes.TryGetValue(nodeId, out var item))
        {
            var parent = DefaultParsingNode(name);
            AddOrUpdateNode(model, parent);
            return model.Nodes[nodeId];
        }

        return item;
    }

    Models.Node GetOrCreateParent(IModel model, Parsing.Node parsedNode)
    {
        var parentName = parsedNode.Properties.Parent;
        if (parentName is null)
        {
            parentName = Parsing.Utils.NodeName.ParseParentName(parsedNode.Name);
            if (
                parentName == ""
                && parsedNode.Properties.Type is not (NodeType.Externals or NodeType.Solution or NodeType.Assembly)
            )
                parentName = ExternalsNodeName;
        }

        var isExternals = parentName == ExternalsNodeName;
        var nodeId = NodeId.FromName(parentName);
        if (!model.Nodes.TryGetValue(nodeId, out var item))
        {
            var parentTyp = isExternals ? Parsing.NodeType.Externals : Parsing.NodeType.Parent;
            var parent = DefaultParentNode(parentName, parentTyp, isExternals ? ExternalsDescription : null);
            AddOrUpdateNode(model, parent);
            return model.Nodes[nodeId];
        }

        // The Externals node is synthesized here (the source parser never emits it), so backfill its
        // description onto an existing node, e.g. one loaded from cache or created before it had one.
        if (isExternals && string.IsNullOrEmpty(item.Description))
            item.Update(DefaultParentNode(parentName, Parsing.NodeType.Externals, ExternalsDescription));

        return item;
    }

    Models.Node GetOrCreateParent(IModel model, NodeDto nodeDto)
    {
        var parentName = nodeDto.ParentName;

        var nodeId = NodeId.FromName(parentName);
        if (!model.Nodes.TryGetValue(nodeId, out var item))
        {
            var parentTyp = Parsing.NodeType.None;
            var parent = DefaultParentNode(parentName, parentTyp);
            AddOrUpdateNode(model, parent);
            return model.Nodes[nodeId];
        }

        return item;
    }

    void EnsureSourceAndTargetExists(IModel model, string sourceName, string targetName)
    {
        if (!model.Nodes.ContainsKey(NodeId.FromName(sourceName)))
        {
            AddOrUpdateNode(model, DefaultParsingNode(sourceName));
        }

        if (!model.Nodes.ContainsKey(NodeId.FromName(targetName)))
        {
            AddOrUpdateNode(model, DefaultParsingNode(targetName));
        }
    }

    static Parsing.Node DefaultParentNode(string name, Parsing.NodeType nodeType, string? description = null) =>
        new(name, new() { Type = nodeType, Description = description });

    static Parsing.Node DefaultParsingNode(string name) => new(name, new() { Type = Parsing.NodeType.None });

    void RemoveNode(IModel model, Models.Node node)
    {
        var parent = node.Parent;
        model.RemoveNode(node);
        if (parent.UpdateStamp != model.UpdateStamp && parent.Children.Count == 0 && !parent.IsRoot)
            RemoveNode(model, parent);
    }
}

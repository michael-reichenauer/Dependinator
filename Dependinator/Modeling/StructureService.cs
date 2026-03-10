using Dependinator.Core.Parsing;
using Dependinator.Modeling.Dtos;
using Dependinator.Modeling.Models;
using Dependinator.Shared.Types;

namespace Dependinator.Modeling;

interface IStructureService
{
    void AddOrUpdateNode(IModel model, Parsing.Node parsedNode);
    void AddOrUpdateLink(IModel model, Parsing.Link parsedLink);
    void ClearNotUpdated(IModel model);
    void SetNodeDto(IModel model, NodeDto nodeDto);
    void SetLinkDto(IModel model, LinkDto linkDto);
    void SetLineLayoutDto(IModel model, LineDto lineLayoutDto);
}

[Transient]
class StructureService(ILineService linesService) : IStructureService
{
    static readonly string ExternalsNodeName = "$Externals";

    public void AddOrUpdateNode(IModel model, Parsing.Node parsedNode)
    {
        if (!model.Nodes.TryGetValue(NodeId.FromName(parsedNode.Name), out var node))
        { // New node, add it to the model and parent
            var parent = GetOrCreateParent(model, parsedNode);

            node = new Models.Node(parsedNode.Name, parent);
            node.Boundary = parent.IsChildrenLayoutCustomized ? Rect.None : NodeLayout.GetNextChildRect(parent);

            node.Update(parsedNode);
            node.UpdateStamp = model.UpdateStamp;

            model.TryAddNode(node);
            parent.AddChild(node);

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

        if (model.Links.TryGetValue(linkId, out var link))
        {
            link.UpdateStamp = model.UpdateStamp;
            link.Source.UpdateStamp = model.UpdateStamp;
            link.Target.UpdateStamp = model.UpdateStamp;
            return;
        }

        EnsureSourceAndTargetExists(model, parsedLink.Source, parsedLink.Target);

        var source = model.Nodes[NodeId.FromName(parsedLink.Source)];
        var target = model.Nodes[NodeId.FromName(parsedLink.Target)];
        if (parsedLink.Properties.TargetType is not null && parsedLink.Properties.TargetType is not NodeType.None)
            target.Type = (NodeType)parsedLink.Properties.TargetType;

        link = new Models.Link(source, target);
        link.UpdateStamp = model.UpdateStamp;

        AddLink(model, link);
    }

    public void SetNodeDto(IModel model, NodeDto nodeDto)
    {
        if (nodeDto.Name == "") // Root node already exists
            return;

        var parent = GetOrCreateParent(model, nodeDto);

        var node = new Models.Node(nodeDto.Name, parent);
        node.SetFromDto(nodeDto);
        if (node.Boundary == Rect.None && !parent.IsChildrenLayoutCustomized)
            node.Boundary = NodeLayout.GetNextChildRect(parent);
        node.UpdateStamp = model.UpdateStamp;

        model.TryAddNode(node);
        parent.AddChild(node);

        return;
    }

    public void SetLinkDto(IModel model, LinkDto linkDto)
    {
        EnsureSourceAndTargetExists(model, linkDto.SourceName, linkDto.TargetName);

        var source = model.Nodes[NodeId.FromName(linkDto.SourceName)];
        var target = model.Nodes[NodeId.FromName(linkDto.TargetName)];
        var targetType = Enums.To<NodeType>(linkDto.TargetType, NodeType.None);
        if (targetType is not NodeType.None)
            target.Type = targetType;

        var link = new Models.Link(source, target);
        link.UpdateStamp = model.UpdateStamp;

        AddLink(model, link);
    }

    public void SetLineLayoutDto(IModel model, LineDto lineLayoutDto)
    {
        if (!model.Lines.TryGetValue(LineId.FromId(lineLayoutDto.LineId), out var line))
            return;

        line.SetSegmentPoints(lineLayoutDto.SegmentPoints);
    }

    public void ClearNotUpdated(IModel model)
    {
        var links = model.Links.Values.Where(l => l.UpdateStamp != model.UpdateStamp).ToList();
        Log.Info($"Remove {links.Count} links");
        foreach (var link in links)
            model.RemoveLink(link);

        var nodes = model.Nodes.Values.Where(n => n.UpdateStamp != model.UpdateStamp && n.Children.Count == 0).ToList();
        Log.Info($"Remove {nodes.Count} nodes");
        foreach (var node in nodes)
            RemoveNode(model, node);
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

        var nodeId = NodeId.FromName(parentName);
        if (!model.Nodes.TryGetValue(nodeId, out var item))
        {
            var parentTyp = parentName == ExternalsNodeName ? Parsing.NodeType.Externals : Parsing.NodeType.Parent;
            var parent = DefaultParentNode(parentName, parentTyp);
            AddOrUpdateNode(model, parent);
            return model.Nodes[nodeId];
        }

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

    static Parsing.Node DefaultParentNode(string name, Parsing.NodeType nodeType) =>
        new(name, new() { Type = nodeType });

    static Parsing.Node DefaultParsingNode(string name) => new(name, new() { Type = Parsing.NodeType.None });

    void RemoveNode(IModel model, Models.Node node)
    {
        var parent = node.Parent;
        model.RemoveNode(node);
        if (parent.UpdateStamp != model.UpdateStamp && parent.Children.Count == 0 && !parent.IsRoot)
            RemoveNode(model, parent);
    }
}

using Dependinator.Core.Parsing;

namespace Dependinator.Models;

interface IStructureService
{
    void TryUpdateNode(Parsing.Node parsedNode);
    void AddOrUpdateNode(Parsing.Node parsedNode);
    void AddOrUpdateLink(Parsing.Link parsedLink);
    void SetNodeDto(NodeDto nodeDto);
    void SetLinkDto(LinkDto linkDto);
    void SetLineLayoutDto(LineDto lineLayoutDto);
}

[Transient]
class StructureService(IModel model, ILineService linesService) : IStructureService
{
    static readonly string ExternalsNodeName = "$Externals";

    public void TryUpdateNode(Parsing.Node parsedNode)
    {
        if (!model.TryGetNode(NodeId.FromName(parsedNode.Name), out var node))
        {
            Log.Info("Failed to find node corresponding to source", parsedNode.Name);
            return; // New node
        }

        node.Update(parsedNode);
        node.UpdateStamp = model.UpdateStamp;
    }

    public void AddOrUpdateNode(Parsing.Node parsedNode)
    {
        if (!model.TryGetNode(NodeId.FromName(parsedNode.Name), out var node))
        { // New node, add it to the model and parent
            var parent = GetOrCreateParent(parsedNode);

            node = new Node(parsedNode.Name, parent);
            node.Boundary = parent.IsChildrenLayoutCustomized ? Rect.None : NodeLayout.GetNextChildRect(parent);

            node.Update(parsedNode);
            node.UpdateStamp = model.UpdateStamp;

            model.AddNode(node);
            parent.AddChild(node);

            return;
        }

        node.Update(parsedNode);
        node.UpdateStamp = model.UpdateStamp;

        var parentName = parsedNode.Properties.Parent;

        if (parentName is not null && node.Parent.Name != parentName)
        { // The node has changed parent, remove it from the old parent and add it to the new parent
            MoveNodeToParent(node, parentName);
        }
    }

    public void AddOrUpdateLink(Parsing.Link parsedLink)
    {
        var linkId = new LinkId(parsedLink.Source, parsedLink.Target);

        if (model.TryGetLink(linkId, out var link))
        {
            link.UpdateStamp = model.UpdateStamp;
            link.Source.UpdateStamp = model.UpdateStamp;
            link.Target.UpdateStamp = model.UpdateStamp;
            return;
        }

        EnsureSourceAndTargetExists(parsedLink.Source, parsedLink.Target);

        var source = model.GetNode(NodeId.FromName(parsedLink.Source));
        var target = model.GetNode(NodeId.FromName(parsedLink.Target));
        if (parsedLink.Properties.TargetType is not null && parsedLink.Properties.TargetType is not NodeType.None)
            target.Type = (NodeType)parsedLink.Properties.TargetType;

        link = new Link(source, target);
        link.UpdateStamp = model.UpdateStamp;

        AddLink(link);
    }

    public void SetNodeDto(NodeDto nodeDto)
    {
        if (nodeDto.Name == "") // Root node already exists
            return;

        var parent = GetOrCreateParent(nodeDto);

        var node = new Node(nodeDto.Name, parent);
        node.SetFromDto(nodeDto);
        if (node.Boundary == Rect.None && !parent.IsChildrenLayoutCustomized)
            node.Boundary = NodeLayout.GetNextChildRect(parent);
        node.UpdateStamp = model.UpdateStamp;

        model.AddNode(node);
        parent.AddChild(node);

        return;
    }

    public void SetLinkDto(LinkDto linkDto)
    {
        EnsureSourceAndTargetExists(linkDto.SourceName, linkDto.TargetName);

        var source = model.GetNode(NodeId.FromName(linkDto.SourceName));
        var target = model.GetNode(NodeId.FromName(linkDto.TargetName));
        var targetType = Enums.To<NodeType>(linkDto.TargetType, NodeType.None);
        if (targetType is not NodeType.None)
            target.Type = targetType;

        var link = new Link(source, target);
        link.UpdateStamp = model.UpdateStamp;

        AddLink(link);
    }

    public void SetLineLayoutDto(LineDto lineLayoutDto)
    {
        if (!model.TryGetLine(LineId.FromId(lineLayoutDto.LineId), out var line))
            return;

        line.SetSegmentPoints(lineLayoutDto.SegmentPoints);
    }

    void MoveNodeToParent(Node node, string parentName)
    {
        // Link lines need to be re-adjusted, so first remove all links and lines
        var lines = node.SourceLines.Concat(node.TargetLines).Concat(node.DirectLines).ToList();
        var links = lines.SelectMany(line => line.Links).ToList();

        links.ForEach(model.RemoveLink);

        node.Parent.RemoveChild(node);
        var parent = GetOrCreateNode(parentName);
        parent.AddChild(node);

        // Re-add link and lines again
        links.ForEach(AddLink);
    }

    void AddLink(Link link)
    {
        model.AddLink(link);
        link.Target.AddTargetLink(link);
        if (link.Source.AddSourceLink(link))
        {
            linesService.AddLinesFromSourceToTarget(link);
        }
    }

    Node GetOrCreateNode(string name)
    {
        var nodeId = NodeId.FromName(name);
        if (!model.TryGetNode(nodeId, out var item))
        {
            var parent = DefaultParsingNode(name);
            AddOrUpdateNode(parent);
            return model.GetNode(nodeId);
        }

        return item;
    }

    Node GetOrCreateParent(Parsing.Node parsedNode)
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
        if (!model.TryGetNode(nodeId, out var item))
        {
            var parentTyp = parentName == ExternalsNodeName ? Parsing.NodeType.Externals : Parsing.NodeType.Parent;
            var parent = DefaultParentNode(parentName, parentTyp);
            AddOrUpdateNode(parent);
            return model.GetNode(nodeId);
        }

        return item;
    }

    Node GetOrCreateParent(NodeDto nodeDto)
    {
        var parentName = nodeDto.ParentName;

        var nodeId = NodeId.FromName(parentName);
        if (!model.TryGetNode(nodeId, out var item))
        {
            var parentTyp = Parsing.NodeType.None;
            var parent = DefaultParentNode(parentName, parentTyp);
            AddOrUpdateNode(parent);
            return model.GetNode(nodeId);
        }

        return item;
    }

    void EnsureSourceAndTargetExists(string sourceName, string targetName)
    {
        if (!model.ContainsKey(NodeId.FromName(sourceName)))
        {
            AddOrUpdateNode(DefaultParsingNode(sourceName));
        }

        if (!model.ContainsKey(NodeId.FromName(targetName)))
        {
            AddOrUpdateNode(DefaultParsingNode(targetName));
        }
    }

    static Parsing.Node DefaultParentNode(string name, Parsing.NodeType nodeType) =>
        new(name, new() { Type = nodeType });

    static Parsing.Node DefaultParsingNode(string name) => new(name, new() { Type = Parsing.NodeType.None });
}

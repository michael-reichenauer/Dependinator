using DependinatorCore.Parsing;

namespace Dependinator.Models;

interface IStructureService
{
    void TryUpdateNode(Parsing.Node parsedNode);
    void AddOrUpdateNode(Parsing.Node parsedNode);
    void AddOrUpdateLink(Parsing.Link parsedLink);
    void SetNodeDto(NodeDto nodeDto);
    void SetLinkDto(LinkDto linkDto);
}

[Transient]
class StructureService(IModel model, ILineService linesService) : IStructureService
{
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
            var parent = GetOrCreateParent(parsedNode.Name, parsedNode.Attributes.IsModule);

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

        // if (parentName is not null && !node.IsRoot && node.Parent.Name != parentName)
        // { // The node has changed parent, remove it from the old parent and add it to the new parent
        //     node.Parent.RemoveChild(node);
        //     var parent = GetOrCreateNode(parentName);
        //     parent.AddChild(node);
        // }
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
        if (parsedLink.Attributes.TargetType is not null && parsedLink.Attributes.TargetType is not NodeType.None)
            target.Type = (NodeType)parsedLink.Attributes.TargetType;

        link = new Link(source, target);
        link.UpdateStamp = model.UpdateStamp;

        model.AddLink(link);
        target.AddTargetLink(link);
        if (source.AddSourceLink(link))
        {
            linesService.AddLinesFromSourceToTarget(link);
        }
        return;
    }

    public void SetNodeDto(NodeDto nodeDto)
    {
        if (nodeDto.Name == "") // Root node already exists
            return;

        //var parentName = nodeDto.ParentName;
        var parent = GetOrCreateParent(nodeDto.Name, nodeDto.Attributes.IsModule);

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

        model.AddLink(link);
        target.AddTargetLink(link);
        if (source.AddSourceLink(link))
        {
            linesService.AddLinesFromSourceToTarget(link);
        }
        return;
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

    Node GetOrCreateParent(string nodeName, bool? isExternal)
    {
        var parentName = GetParentName(nodeName, isExternal);
        var nodeId = NodeId.FromName(parentName);
        if (!model.TryGetNode(nodeId, out var item))
        {
            var parent = DefaultParentNode(parentName);
            AddOrUpdateNode(parent);
            return model.GetNode(nodeId);
        }

        return item;
    }

    string GetParentName(string nodeName, bool? isExternal)
    {
        var parentName = Parsing.Utils.NodeName.ParseParentName(nodeName);

        return parentName;
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

    static Parsing.Node DefaultParentNode(string name) =>
        new(name, new() { Type = Parsing.NodeType.Parent, Parent = Parsing.Utils.NodeName.ParseParentName(name) });

    static Parsing.Node DefaultParsingNode(string name) =>
        new(name, new() { Type = Parsing.NodeType.None, Parent = Parsing.Utils.NodeName.ParseParentName(name) });
}

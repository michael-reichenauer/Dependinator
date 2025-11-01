using ICSharpCode.Decompiler.CSharp.Syntax;

namespace Dependinator.Models;

interface IStructureService
{
    void AddOrUpdateNode(Parsing.Node parsedNode);
    void AddOrUpdateLink(Parsing.Link parsedLink);
    void SetNodeDto(NodeDto nodeDto);
    void SetLinkDto(LinkDto linkDto);
}

[Transient]
class StructureService(IModel model, ILineService linesService) : IStructureService
{
    public void AddOrUpdateNode(Parsing.Node parsedNode)
    {
        var parentName = parsedNode.Attributes.Parent;
        if (!model.TryGetNode(NodeId.FromName(parsedNode.Name), out var node))
        { // New node, add it to the model and parent
            var parent = GetOrCreateParent(parentName);

            node = new Node(parsedNode.Name, parent);
            node.Boundary = NodeLayout.GetNextChildRect(parent);

            node.Update(parsedNode);
            node.UpdateStamp = model.UpdateStamp;

            model.AddNode(node);
            parent.AddChild(node);

            return;
        }

        node.Update(parsedNode);
        node.UpdateStamp = model.UpdateStamp;

        if (!node.IsRoot && node.Parent.Name != parentName)
        { // The node has changed parent, remove it from the old parent and add it to the new parent
            node.Parent.RemoveChild(node);
            var parent = GetOrCreateNode(parentName);
            parent.AddChild(node);
        }
    }

    public void AddOrUpdateLink(Parsing.Link parsedLink)
    {
        var linkId = new LinkId(parsedLink.Source, parsedLink.Target);

        if (model.TryGetLink(linkId, out var link))
        {
            link.UpdateStamp = model.UpdateStamp;
            return;
        }

        EnsureSourceAndTargetExists(parsedLink.Source, parsedLink.Target);

        var source = model.GetNode(NodeId.FromName(parsedLink.Source));
        var target = model.GetNode(NodeId.FromName(parsedLink.Target));

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

        var parentName = nodeDto.ParentName;
        var parent = GetOrCreateParent(parentName);

        var node = new Node(nodeDto.Name, parent);
        if (nodeDto.Boundary is null)
        {
            node.Boundary = NodeLayout.GetNextChildRect(parent);
        }
        node.SetFromDto(nodeDto);
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

    Node GetOrCreateParent(string name)
    {
        var nodeId = NodeId.FromName(name);
        if (!model.TryGetNode(nodeId, out var item))
        {
            var parent = DefaultParentNode(name);
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

    static Parsing.Node DefaultParentNode(string name) =>
        new(name, new() { Type = Parsing.NodeType.Parent, Parent = Parsing.NodeName.ParseParentName(name) });

    static Parsing.Node DefaultParsingNode(string name) =>
        new(name, new() { Type = Parsing.NodeType.None, Parent = Parsing.NodeName.ParseParentName(name) });
}

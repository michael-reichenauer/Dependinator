namespace Dependinator.Models;

interface IStructureService
{
    void AddOrUpdateNode(Parsing.Node parsedNode);
    void AddOrUpdateLink(Parsing.Link parsedLink);
    void SetNodeDto(NodeDto nodeDto);
    void SetLinkDto(LinkDto linkDto);
}

[Transient]
class StructureService(IModel model) : IStructureService
{
    public void AddOrUpdateNode(Parsing.Node parsedNode)
    {
        var parentName = parsedNode.ParentName;
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
        var linkId = new LinkId(parsedLink.SourceName, parsedLink.TargetName);

        if (model.TryGetLink(linkId, out var link))
        {
            link.UpdateStamp = model.UpdateStamp;
            return;
        }

        EnsureSourceAndTargetExists(parsedLink.SourceName, parsedLink.TargetName);

        var source = model.GetNode(NodeId.FromName(parsedLink.SourceName));
        var target = model.GetNode(NodeId.FromName(parsedLink.TargetName));

        link = new Link(source, target);
        link.UpdateStamp = model.UpdateStamp;

        model.AddLink(link);
        target.AddTargetLink(link);
        if (source.AddSourceLink(link))
        {
            AddLinesFromSourceToTarget(link);
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
            AddLinesFromSourceToTarget(link);
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

    void AddLinesFromSourceToTarget(Link link)
    {
        Node commonAncestor = GetCommonAncestor(link);

        // Add lines from source and target nodes upp to its parent for all ancestors until just before the common ancestor
        var sourceAncestor = AddAncestorLines(link, link.Source, commonAncestor);
        var targetAncestor = AddDescendantLines(link, link.Target, commonAncestor);

        // Connect 'sibling' nodes that are ancestors to source and target (or are source/target if they are siblings)
        AddDirectLine(sourceAncestor, targetAncestor, link);
    }

    static Node GetCommonAncestor(Link link)
    {
        var targetAncestors = link.Target.Ancestors().ToList();
        return link.Source.Ancestors().First(targetAncestors.Contains);
    }

    Node AddAncestorLines(Link link, Node source, Node commonAncestor)
    {
        // Add lines from source node upp to all ancestors until just before common ancestors
        Node currentSource = source;
        foreach (var parent in source.Ancestors())
        {
            if (parent == commonAncestor)
                break;
            AddDirectLine(currentSource, parent, link);
            currentSource = parent;
        }

        return currentSource;
    }

    Node AddDescendantLines(Link link, Node target, Node commonAncestor)
    {
        // Add lines from just below commonAncestor node down to all descendants until target
        Node currentTarget = target;
        foreach (var parent in target.Ancestors())
        {
            if (parent == commonAncestor)
                break;
            AddDirectLine(parent, currentTarget, link);
            currentTarget = parent;
        }

        return currentTarget;
    }

    void AddDirectLine(Node source, Node target, Link link)
    {
        var line = source.SourceLines.FirstOrDefault(l => l.Target == target);
        if (line == null)
        { // First line between these source and target
            line = new Line(source, target);
            source.SourceLines.Add(line);
            target.TargetLines.Add(line);

            model.AddLine(line);
        }

        line.Add(link);
        link.AddLine(line);
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
        new(name, Parsing.Node.ParseParentName(name), Parsing.NodeType.Parent, Parsing.NodeAttributes.Default);

    static Parsing.Node DefaultParsingNode(string name) =>
        new(name, Parsing.Node.ParseParentName(name), Parsing.NodeType.None, Parsing.NodeAttributes.Default);
}

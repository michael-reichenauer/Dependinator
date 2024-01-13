namespace Dependinator.Models;


interface IModelStructureService
{
    void AddOrUpdateNode(Parsing.Node parsedNode);
    void AddOrUpdateLink(Parsing.Link parsedLink);
}


[Transient]
class ModelStructureService : IModelStructureService
{
    readonly IModel model;

    public ModelStructureService(IModel model)
    {
        this.model = model;
    }

    public void AddOrUpdateNode(Parsing.Node parsedNode)
    {
        if (!model.TryGetNode(NodeId.FromName(parsedNode.Name), out var node))
        {   // New node, add it to the model and parent
            var parentName = parsedNode.ParentName;
            var parent = GetOrCreateParent(parentName);

            var boundary = NodeLayout.GetNextChildRect(parent);
            node = new Node(parsedNode.Name, parent)
            {
                Type = parsedNode.Type,
                Description = parsedNode.Description,
                Boundary = boundary,
            };

            model.AddNode(node);
            parent.AddChild(node);

            return;
        }

        if (node.Update(parsedNode))
        {
            var parentName = parsedNode.ParentName;
            if (node.Parent.Name != parentName)
            {   // The node has changed parent, remove it from the old parent and add it to the new parent
                node.Parent.RemoveChild(node);
                var parent = GetOrCreateNode(parentName);
                parent.AddChild(node);
            }
        }
    }

    public void AddOrUpdateLink(Parsing.Link parsedLink)
    {
        var linkId = new LinkId(parsedLink.SourceName, parsedLink.TargetName);
        if (model.ContainsKey(linkId)) return;

        EnsureSourceAndTargetExists(parsedLink);

        var source = model.GetNode(NodeId.FromName(parsedLink.SourceName));
        var target = model.GetNode(NodeId.FromName(parsedLink.TargetName));
        var link = new Link(source, target);

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
        var targetAncestor = AddAncestorLines(link, link.Target, commonAncestor);

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
            if (parent == commonAncestor) break;
            AddDirectLine(currentSource, parent, link);
            currentSource = parent;
        }

        return currentSource;
    }


    void AddDirectLine(Node source, Node target, Link link)
    {
        var line = source.SourceLines.FirstOrDefault(l => l.Target == target);
        if (line == null)
        {   // First line between these source and target
            line = new Line(source, target);
            source.SourceLines.Add(line);
            target.TargetLines.Add(line);

            model.AddLine(line);
        }

        line.Add(link);
        link.AddLine(line);
    }


    void EnsureSourceAndTargetExists(Parsing.Link parsedLink)
    {
        if (!model.ContainsKey(NodeId.FromName(parsedLink.SourceName)))
        {
            AddOrUpdateNode(DefaultParsingNode(parsedLink.SourceName));
        }

        if (!model.ContainsKey(NodeId.FromName(parsedLink.TargetName)))
        {
            AddOrUpdateNode(DefaultParsingNode(parsedLink.TargetName));
        }
    }


    static Parsing.Node DefaultParentNode(string name) =>
        new(name, Parsing.Node.ParseParentName(name), Parsing.NodeType.Parent, "");
    static Parsing.Node DefaultParsingNode(string name) =>
        new(name, Parsing.Node.ParseParentName(name), Parsing.NodeType.None, "");

}

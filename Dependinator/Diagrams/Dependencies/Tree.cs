
using Dependinator.Models;

namespace Dependinator.Diagrams;


public enum TreeSide { Left, Right }


internal record Tree
{
    TreeItem selected = null!;

    public Tree(TreeSide side, DependenciesService service, Node root)
    {
        Side = side;
        Service = service;
        Root = new TreeItem(this)
        {
            Title = "<all>",
            Icon = Dependinator.DiagramIcons.Icon.GetIcon(root.Type.Text),
            NodeId = root.Id,
            NodeChildrenCount = root.Children.Count,
        };
        Items.Add(Root);
    }

    public TreeSide Side { get; }
    public DependenciesService Service { get; }
    public HashSet<TreeItem> Items { get; set; } = [];
    public TreeItem Root { get; }


    public TreeItem Selected
    {
        get => selected;
        set
        {
            if (value == null || value == selected) return;
            Service.ItemSelected(value);
            selected = value;
        }
    }


    public TreeItem AddNode(Node node)
    {
        // Add Ancestors to the node
        // Start from root, but skip root
        var ancestors = node.Ancestors().Reverse().Skip(1);
        var current = Root;
        foreach (var ancestor in ancestors)
        {
            var ancestorItem = current.Items.FirstOrDefault(n => n.NodeId == ancestor.Id);
            if (ancestorItem != null)
            {   // Ancestor already added
                current = ancestorItem;
                continue;
            }

            // Add ancestor node to tree
            current = current.AddChildNode(ancestor);
        }

        // Add node to its parent
        return current.AddChildNode(node);
    }
}

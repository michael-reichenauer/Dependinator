namespace Dependinator.Diagrams;


public enum TreeSide { Left, Right }


internal class Tree
{
    TreeItem selected = null!;

    public HashSet<TreeItem> Items { get; set; } = new();
    public TreeItem Selected
    {
        get => selected;
        set
        {
            if (value == null || value == selected) return;
            if (selected != null) selected.IsSelected = false;
            selected = value;
            selected.IsSelected = true;
        }
    }
}

using Dependinator.Diagrams;


namespace Dependinator.Model;



internal class DataNode : Equatable<DataNode>, IDataItem
{
    public DataNode(
        DataNodeName name,
        DataNodeName parent,
        NodeType nodeType)
    {
        Name = name;
        Parent = parent;
        NodeType = nodeType;

        IsEqualWhenSame(Name);
    }


    public DataNodeName Name { get; }
    public DataNodeName Parent { get; }
    public NodeType NodeType { get; }


    public string Description { get; set; }
    public Rect Bounds { get; set; } = new Rect(0, 0, 0, 0);
    public double Scale { get; set; }
    public string Color { get; set; }
    public string ShowState { get; set; }
    public bool IsModified { get; set; }
    public bool HasModifiedChild { get; set; }
    public bool HasParentModifiedChild { get; set; }
    public bool IsQueued { get; set; }


    public override string ToString() => Name.ToString();
}


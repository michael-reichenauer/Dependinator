namespace Dependinator.Models;

class Link : IItem
{
    public Link(Node source, Node target)
    {
        Source = source;
        Target = target;
    }

    public Node Source { get; }
    public Node Target { get; }
}


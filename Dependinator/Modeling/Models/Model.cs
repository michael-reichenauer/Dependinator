using Dependinator.Core.Parsing;
using Dependinator.Modeling.Persistence;
using Dependinator.Shared.Types;

namespace Dependinator.Modeling.Models;

interface IModel : IDisposable
{
    DateTime UpdateStamp { get; set; }
    string Path { get; set; }
    Node Root { get; }
    Rect ViewRect { get; set; }
    double Zoom { get; set; }
    Pos Offset { get; set; }

    IReadOnlyDictionary<NodeId, Node> Nodes { get; }
    IReadOnlyDictionary<LinkId, Link> Links { get; }
    IReadOnlyDictionary<LineId, Line> Lines { get; }

    void TryAddNode(Node node);
    void TryAddLink(Link link);
    void TryAddLine(Line line);

    void RemoveNode(Node node);
    void RemoveLink(Link link);
    void RemoveLine(Line line);

    void Clear();

    ModelDto SerializeToDto();
    void SetFromDto(string path, ModelDto modelDto);
}

class Model : IModel
{
    readonly Action disposeAction;

    public Model(Action disposeAction)
    {
        this.disposeAction = disposeAction;
        InitModel();
    }

    public void Dispose() => disposeAction();

    public string Path { get; set; } = "";
    public DateTime UpdateStamp { get; set; }
    public Rect ViewRect { get; set; } = Rect.None;
    public double Zoom { get; set; } = 0;
    public Pos Offset { get; set; } = Pos.None;

    public Node Root { get; private set; } = null!;

    Dictionary<NodeId, Node> nodes { get; } = [];
    Dictionary<LinkId, Link> links { get; } = [];
    Dictionary<LineId, Line> lines { get; } = [];

    public IReadOnlyDictionary<NodeId, Node> Nodes => nodes;
    public IReadOnlyDictionary<LinkId, Link> Links => links;
    public IReadOnlyDictionary<LineId, Line> Lines => lines;

    public ModelDto SerializeToDto() =>
        new()
        {
            Name = Path,
            Zoom = Zoom,
            Offset = Offset,
            ViewRect = ViewRect,
            Nodes = [.. nodes.Values.Select(n => n.ToDto())],
            Links = [.. links.Values.Select(l => l.ToDto())],
            Lines =
            [
                .. lines
                    .Values.Where(l => !l.IsDirect && l.SegmentPoints.Count > 0)
                    .Select(l => new LineDto() { LineId = l.Id.Value, SegmentPoints = [.. l.SegmentPoints] }),
            ],
        };

    public void SetFromDto(string path, ModelDto modelDto)
    {
        Path = path;
        Zoom = modelDto.Zoom;
        Offset = modelDto.Offset;
        ViewRect = modelDto.ViewRect;
        // Nodes and links will be set by model service in separate worker thread
    }

    public void TryAddNode(Node node)
    {
        if (nodes.ContainsKey(node.Id))
            return;
        nodes[node.Id] = node;
    }

    public void TryAddLink(Link link)
    {
        if (links.ContainsKey(link.Id))
            return;
        links[link.Id] = link;
    }

    public void TryAddLine(Line line)
    {
        if (lines.ContainsKey(line.Id))
            return;
        lines[line.Id] = line;
    }

    public void Clear()
    {
        nodes.Clear();
        links.Clear();
        lines.Clear();
        Path = "";
        ViewRect = Rect.None;
        Zoom = 0;
        Offset = Pos.None;

        InitModel();
    }

    public void RemoveNode(Node node)
    {
        var parent = node.Parent;
        nodes.Remove(node.Id);
        parent?.RemoveChild(node);
    }

    public void RemoveLink(Link link)
    {
        links.Remove(link.Id);

        foreach (var line in link.Lines)
        {
            line.Remove(link);
            if (line.IsEmpty)
                RemoveLine(line);

            link.Target.Remove(link);
            link.Source.Remove(link);
        }
    }

    public void RemoveLine(Line line)
    {
        lines.Remove(line.Id);
        line.RenderAncestor?.RemoveDirectLine(line);
        line.Target.Remove(line);
        line.Source.Remove(line);
    }

    void InitModel()
    {
        Root = new("", null!)
        {
            Type = NodeType.Root,
            Boundary = new Rect(0, 0, 1000, 1000),
            ContainerZoom = 1,
        };

        nodes[Root.Id] = Root;
    }
}

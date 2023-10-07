using DependinatorLib.Diagrams.Elements;


namespace DependinatorLib.Diagrams;


public interface ICanvasService
{
    string SvgContent { get; }
    Task InitAsync(Canvas canvas);
}


[Scoped]
class CanvasService : ICanvasService
{
    readonly List<IElement> elements = new List<IElement>();
    Canvas canvas = null!;

    public CanvasService()
    {
    }

    public string SvgContent { get; private set; } = "";

    public Task InitAsync(Canvas canvas)
    {
        this.canvas = canvas;

        elements.Add(new Node { X = 90, Y = 90, W = 40, H = 40, Color = "#00aa00" });
        elements.Add(new Node { X = 190, Y = 190, W = 40, H = 40, Color = "#00aa00" });
        elements.Add(new Connector { X1 = 120, Y1 = 130, X2 = 220, Y2 = 190, Color = "#555555" });
        Update();

        return Task.CompletedTask;
    }


    public void Update()
    {
        elements.ForEach(n => n.Update());
        SvgContent = elements.Select(n => n.Svg).Join("\n");
        canvas?.TriggerStateHasChanged();
    }
}
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
    readonly IPanZoomService panZoomService;
    readonly List<IElement> elements = new List<IElement>();
    Rect bounds = new(0, 0, 0, 0);
    Canvas canvas = null!;

    public CanvasService(IPanZoomService panZoomService)
    {
        this.panZoomService = panZoomService;
    }

    public string SvgContent { get; private set; } = "";

    public Task InitAsync(Canvas canvas)
    {
        this.canvas = canvas;
        GenerateElements();

        Update();

        return Task.CompletedTask;
    }


    public void Update()
    {
        elements.ForEach(n => n.Update());
        bounds = new Rect(
            elements.Select(n => n.X).Min(),
            elements.Select(n => n.Y).Min(),
            elements.Select(n => n.X + n.W).Max(),
            elements.Select(n => n.Y + n.H).Max());

        SvgContent = elements.Select(n => n.Svg).Join("\n");

        panZoomService.PanZoomToFit(bounds);
        canvas?.TriggerStateHasChanged();
    }

    void GenerateElements()
    {
        // Generating random elements and connectors
        var random = new Random();
        for (int i = 0; i < 100; i++)
        {
            AddElement(new Node
            {
                X = random.Next(0, 1000),
                Y = random.Next(0, 1000),
                W = random.Next(20, 100),
                H = random.Next(20, 100),
                Color = $"#{random.Next(0, 256):x2}{random.Next(0, 256):x2}{random.Next(0, 256):x2}",
                Background = $"#{random.Next(0, 256):x2}{random.Next(0, 256):x2}{random.Next(0, 256):x2}"
            });
        }

        for (int i = 0; i < 30; i++)
        {
            AddElement(new Connector
            {
                X1 = random.Next(0, 1000),
                Y1 = random.Next(0, 1000),
                X2 = random.Next(0, 1000),
                Y2 = random.Next(0, 1000),
                Color = $"#{random.Next(0, 256):x2}{random.Next(0, 256):x2}{random.Next(0, 256):x2}"
            });
        }
    }

    void AddElement(IElement element)
    {
        elements.Add(element);
    }
}
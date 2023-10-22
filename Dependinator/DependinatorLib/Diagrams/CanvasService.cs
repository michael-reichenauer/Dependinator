using Dependinator.Diagrams.Elements;



namespace Dependinator.Diagrams;


interface ICanvasService
{
    string SvgContent { get; }
    Task InitAsync(Canvas canvas);

    void Refresh();
    void Refresh2();
}


[Scoped]
class CanvasService : ICanvasService
{
    readonly IPanZoomService panZoomService;
    private readonly Models.IModelService modelService;
    readonly List<IElement> elements = new List<IElement>();
    Rect bounds = new(0, 0, 0, 0);

    string svgContent = "";
    public string SvgContent
    {
        get
        {
            //Log.Info("Get SvgContent");
            return svgContent;
        }
        private set
        {
            Log.Info("Set SvgContent");
            svgContent = value;
        }
    }

    Canvas canvas = null!;

    public CanvasService(IPanZoomService panZoomService, Models.IModelService modelService)
    {
        this.panZoomService = panZoomService;
        this.modelService = modelService;
    }


    public Task InitAsync(Canvas canvas)
    {
        this.canvas = canvas;
        GenerateElements();

        Update();

        return Task.CompletedTask;
    }


    public void Refresh()
    {
        Log.Info("Refresh");
        Update();
    }

    public void Refresh2()
    {
        modelService.Refresh();
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

        for (int i = 0; i < 10; i++)
        {
            AddElement(new Connector
            {
                X1 = random.Next(0, 1000),
                Y1 = random.Next(0, 1000),
                X2 = random.Next(0, 1000),
                Y2 = random.Next(0, 1000),
                More = new List<Pos>
                {
                    new Pos(random.Next(0, 1000), random.Next(0, 1000)),
                    new Pos(random.Next(0, 1000), random.Next(0, 1000)),
                    new Pos(random.Next(0, 1000), random.Next(0, 1000)),
                },
                Color = $"#{random.Next(0, 256):x2}{random.Next(0, 256):x2}{random.Next(0, 256):x2}"
            });
        }
    }

    void AddElement(IElement element)
    {
        elements.Add(element);
    }
}
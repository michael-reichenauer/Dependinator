using Dependinator.Diagrams.Elements;
using Dependinator.Models;



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
    private readonly IModelDb modelDb;
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

    public CanvasService(IPanZoomService panZoomService, Models.IModelService modelService, IModelDb modelDb)
    {
        this.panZoomService = panZoomService;
        this.modelService = modelService;
        this.modelDb = modelDb;
    }


    public Task InitAsync(Canvas canvas)
    {
        this.canvas = canvas;
        //GenerateElements();

        Update();

        return Task.CompletedTask;
    }


    public void Refresh()
    {
        Log.Info("Refresh");
        Update();
    }

    public async void Refresh2()
    {
        using var _ = Timing.Start();
        await modelService.RefreshAsync();

        using (var context = modelDb.GetModel())
        {
            using var __ = Timing.Start("Generate elements");
            var model = context.Model;
            var root = model.Root;
            SvgContent = root.ContentSvg;

            var nodes = model.Items.Values.OfType<Models.Node>().ToList();

            bounds = new Rect(
                nodes.Select(n => n.Rect.X).Min(),
                nodes.Select(n => n.Rect.Y).Min(),
                nodes.Select(n => n.Rect.X + n.Rect.Width).Max(),
                nodes.Select(n => n.Rect.Y + n.Rect.Height).Max());
        }

        panZoomService.PanZoomToFit(bounds);
        canvas?.TriggerStateHasChanged();
    }


    public void Update()
    {
        bounds = new Rect(0, 0, 30, 30);

        SvgContent = elements.Select(n => n.Svg).Join("\n");

        panZoomService.PanZoomToFit(bounds);
        canvas?.TriggerStateHasChanged();
    }
}
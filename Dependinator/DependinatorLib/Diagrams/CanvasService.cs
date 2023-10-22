using Dependinator.Models;

namespace Dependinator.Diagrams;


interface ICanvasService
{
    string SvgContent { get; }
    Task InitAsync(Canvas canvas);

    void Refresh();
    void Clear();
}


[Scoped]
class CanvasService : ICanvasService
{
    readonly IPanZoomService panZoomService;
    private readonly IModelService modelService;
    private readonly IModelDb modelDb;

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

        return Task.CompletedTask;
    }


    public void Clear()
    {
        using var _ = Timing.Start();
        using var context = modelDb.GetModel();
        context.Model.Clear();
        SvgContent = context.Model.Root.ContentSvg;
        canvas?.TriggerStateHasChanged();
    }

    public async void Refresh()
    {
        using var _ = Timing.Start();
        await modelService.RefreshAsync();

        using (var context = modelDb.GetModel())
        {
            using var __ = Timing.Start("Generate elements");
            var model = context.Model;
            var root = model.Root;

            SvgContent = root.ContentSvg;
            bounds = model.Root.TotalRect;
        }

        panZoomService.PanZoomToFit(bounds);
        canvas?.TriggerStateHasChanged();
    }
}
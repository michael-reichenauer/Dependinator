namespace Dependinator.Models;


interface IModelSvgService
{
    (Svgs, Rect) GetSvg();
}


[Transient]
class ModelSvgService : IModelSvgService
{
    readonly IModel model;

    public ModelSvgService(IModel model)
    {
        this.model = model;
    }

    public (Svgs, Rect) GetSvg()
    {
        using var t = Timing.Start();

        var svgs = new List<Level>();

        for (int i = 0; i < 100; i++)
        {
            var zoom = i == 0 ? 1.0 : Math.Pow(2, i);
            var svg = model.Root.GetSvg(Pos.Zero, zoom);
            if (svg == "") break;
            svgs.Add(new Level(svg, 1 / zoom));
            // Log.Info($"Level: #{i} zoom: {zoom} svg: {svg.Length} chars");
        }
        Log.Info($"Levels: {svgs.Count}");

        var totalBoundary = model.Root.TotalBoundary;
        return (new Svgs(svgs), totalBoundary);
    }
}

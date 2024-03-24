using System.Text.Json;
using Dependinator.Models;
using Dependinator.Shared;

namespace Dependinator.Parsing;


interface IPersistenceService
{
    Model ModelToData(Models.IModel node);
    Task<R> WriteAsync(Model model);
    Task<R<Model>> ReadAsync(string path);
}


[Transient]
class PersistenceService : IPersistenceService
{
    readonly IFileService fileService;
    const string modelName = "DependinatorModel.json";

    public PersistenceService(IFileService fileService)
    {
        this.fileService = fileService;
    }

    public Model ModelToData(Models.IModel model)
    {
        var data = new Model
        {
            Path = model.Path,
            Zoom = model.Zoom,
            ViewRect = model.ViewRect,
            Nodes = model.Items.Values.OfType<Models.Node>().Select(ToNode).ToList(),
            Links = model.Items.Values.OfType<Models.Link>().Select(ToLink).ToList(),
        };

        return data;
    }

    public Task<R> WriteAsync(Model model)
    {
        return Task.Run(async () =>
        {
            var filePath = model.Path;
            using var t = Timing.Start($"Write model '{filePath}'");

            await fileService.WriteAsync(filePath, model);

            // if (!Build.IsWebAssembly)
            // {
            //     var json = JsonSerializer.Serialize(model,
            //     new JsonSerializerOptions
            //     {
            //         WriteIndented = true,
            //         IgnoreNullValues = true
            //     });
            //     var path = GetModelFilePath();
            //     if (!Try(out var e, () => File.WriteAllText(path, json))) return e;
            // }

            return R.Ok;
        });
    }

    public Task<R<Model>> ReadAsync(string modelPath)
    {
        return Task.Run<R<Model>>(async () =>
        {
            var filePath = modelPath;
            using var t = Timing.Start($"Read model '{filePath}'");

            if (modelPath == ExampleModel.Path)
            {
                if (!Try(out var model, out var e, await fileService.ReadAsync<Model>(filePath)))
                {
                    var json = ExampleModel.Model;
                    if (!Try(out model, out e, () => JsonSerializer.Deserialize<Model>(json))) return e;
                }

                return model;
            }

            if (!Try(out var model2, out var e2, await fileService.ReadAsync<Model>(filePath))) return e2;
            return model2;
        });
    }


    static Node ToNode(Models.Node node) =>
        new(node.Name, node.Parent?.Name ?? "", new NodeType(node.Type.Text), node.Description)
        {
            X = node.Boundary != Rect.None ? node.Boundary.X : null,
            Y = node.Boundary != Rect.None ? node.Boundary.Y : null,
            Width = node.Boundary != Rect.None ? node.Boundary.Width : null,
            Height = node.Boundary != Rect.None ? node.Boundary.Height : null,
            Zoom = node.ContainerZoom != Models.Node.DefaultContainerZoom ? node.ContainerZoom : null,
            Color = node.Color,
            Background = node.Background,
        };


    static Link ToLink(Models.Link link) =>
        new(link.Source.Name, link.Target.Name, new NodeType(link.Target.Type.Text));

    static string GetModelFilePath() =>
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), modelName);
}

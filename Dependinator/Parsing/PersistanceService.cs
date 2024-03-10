
using System.Text.Json;
using Dependinator.Models;

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
        var data = new Model();
        data.Nodes.AddRange(model.Items.Values.OfType<Models.Node>().Select(ToNode));
        data.Links.AddRange(model.Items.Values.OfType<Models.Link>().Select(ToLink));
        return data;
    }

    public Task<R> WriteAsync(Model model)
    {
        return Task.Run(async () =>
        {
            using var t = Timing.Start("Write model");

            await fileService.WriteAsync(modelName, model);

            // var path = GetModelFilePath();
            // if (!Try(out var e, () => File.WriteAllText(path, json))) return e;

            return R.Ok;
        });
    }

    public Task<R<Model>> ReadAsync(string path)
    {
        return Task.Run<R<Model>>(async () =>
        {
            using var t = Timing.Start("Read model");

            if (!Try(out var model, out var e, await fileService.ReadAsync<Model>(modelName)))
            {
                if (e.IsNone)
                {
                    var json = ExampleModel.Model;
                    Log.Info("Read example json", json.Length, "bytes", t.ToString());

                    if (!Try(out model, out var ee, () => JsonSerializer.Deserialize<Model>(json))) return ee;
                    return model;
                }

                return e;
            }

            return model;

            //  return R.Error("Failed to load model");

            // path = path == "" ? GetModelFilePath() : path;
            // var json = ExampleModel.Model;

            // // Log.Info("Read persistance", path);
            // // if (path != "Example.exe")
            // // {
            // //     if (!Try(out json, out var e, () => File.ReadAllText(path))) return e;
            // // }
            // Log.Info("Read json", json.Length, "bytes", t.ToString());

            // if (!Try(out var model, out var ee, () => JsonSerializer.Deserialize<Model>(json))) return ee;
            // Log.Info("Parsed json", json.Length, "bytes", t.ToString());

            // return model;
        });
    }


    static Node ToNode(Models.Node node) =>
        new(node.Name, node.Parent?.Name ?? "", new NodeType(node.Type.Text), node.Description)
        {
            X = node.Boundary.X,
            Y = node.Boundary.Y,
            Width = node.Boundary.Width,
            Height = node.Boundary.Height,
            Zoom = node.ContainerZoom,
            Color = node.Color,
            Background = node.Background,
        };


    static Link ToLink(Models.Link link) =>
     new(link.Source.Name, link.Target.Name, new NodeType(link.Target.Type.Text));

    static string GetModelFilePath() =>
        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), modelName);
}

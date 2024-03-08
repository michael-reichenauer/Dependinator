
using System.Text.Json;
using Dependinator.Models;

namespace Dependinator.Parsing;


interface IPersistenceService
{
    Model ModelToData(Models.IModel node);
    Task<R> SaveAsync(Model model);
    Task<R<Model>> LoadAsync(string path);
}


[Transient]
class PersistenceService : IPersistenceService
{
    static readonly JsonSerializerOptions indented = new() { WriteIndented = true };
    private readonly IDatabase database;
    const string modelName = "DependinatorModel.json";

    public PersistenceService(IDatabase database)
    {
        this.database = database;
    }

    public Model ModelToData(Models.IModel model)
    {
        using var t = Timing.Start();
        var data = new Model();
        data.Nodes.AddRange(model.Items.Values.OfType<Models.Node>().Select(ToNode));
        data.Links.AddRange(model.Items.Values.OfType<Models.Link>().Select(ToLink));
        return data;
    }

    public Task<R> SaveAsync(Model model)
    {
        return Task.Run(async () =>
        {
            using var t = Timing.Start("Save model to file xd");

            await database.SetAsync(modelName, model);

            // var path = GetModelFilePath();
            // if (!Try(out var e, () => File.WriteAllText(path, json))) return e;

            return R.Ok;
        });
    }

    public Task<R<Model>> LoadAsync(string path)
    {
        return Task.Run<R<Model>>(async () =>
        {
            Log.Info("Loading value ...", path);
            using var t = Timing.Start("Load model from file");

            if (!Try(out var model, out var e, await database.GetAsync<Model>(modelName)))
            {
                if (e.IsNone)
                {
                    var json = ExampleModel.Model;
                    Log.Info("Read example json", json.Length, "bytes", t.ToString());

                    if (!Try(out model, out var ee, () => JsonSerializer.Deserialize<Model>(json))) return ee;
                    Log.Info("Parsed json", json.Length, "bytes", t.ToString());
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

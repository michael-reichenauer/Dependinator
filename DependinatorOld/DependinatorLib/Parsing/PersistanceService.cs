
using System.Text.Json;

namespace Dependinator.Parsing;


interface IPersistenceService
{
    Model ModelToData(Models.IModel node);
    Task<R> SaveAsync(Model model);
    Task<R<Model>> LoadAsync();
}


[Transient]
class PersistenceService : IPersistenceService
{
    static readonly JsonSerializerOptions indented = new() { WriteIndented = true };
    const string modelName = "DependinatorModel.json";

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
        return Task.Run(() =>
        {
            using var t = Timing.Start("Save model to file");
            string json = JsonSerializer.Serialize(model, indented);

            var path = GetModelFilePath();
            if (!Try(out var e, () => File.WriteAllText(path, json))) return e;

            return R.Ok;
        });
    }

    public Task<R<Model>> LoadAsync()
    {
        return Task.Run<R<Model>>(() =>
        {
            using var t = Timing.Start("Load model from file");

            var path = GetModelFilePath();
            if (!Try(out var json, out var e, () => File.ReadAllText(path))) return e;

            if (!Try(out var model, out e, () => JsonSerializer.Deserialize<Model>(json))) return e;

            return model;
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

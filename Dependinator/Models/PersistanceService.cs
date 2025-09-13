using Dependinator.Models;

namespace Dependinator.Parsing;

interface IPersistenceService
{
    Task<R> WriteAsync(ModelDto model);
    Task<R<ModelDto>> ReadAsync(string path);
}

record ModelDto
{
    public static string CurrentVersion = "2";

    public string FormatVersion { get; init; } = CurrentVersion;
    public required string Path { get; init; }
    public double Zoom { get; init; } = 0;
    public Pos Offset { get; init; } = Pos.None;
    public Rect ViewRect { get; init; } = Rect.None;
    public required IReadOnlyList<NodeDto> Nodes { get; init; }
    public required IReadOnlyList<LinkDto> Links { get; init; }

    public static ModelDto From(Models.IModel model) =>
        new()
        {
            Path = model.Path,
            Zoom = model.Zoom,
            Offset = model.Offset,
            ViewRect = model.ViewRect,
            Nodes = [.. model.Items.Values.OfType<Models.Node>().Select(NodeDto.From)],
            Links = [.. model.Items.Values.OfType<Models.Link>().Select(LinkDto.From)],
        };
}

record NodeDto : IItem
{
    public required string Name { get; init; }
    public required string ParentName { get; init; }
    public required string Type { get; init; }
    public string? Description { get; init; }
    public Rect? Boundary { get; init; }
    public double? Zoom { get; init; }
    public Pos? Offset { get; init; }
    public string? Color { get; init; }

    public static NodeDto From(Models.Node node) =>
        new()
        {
            Name = node.Name,
            ParentName = node.Parent?.Name ?? "",
            Type = node.Type.Text,
            Description = node.Description,
            Boundary = node.Boundary != Rect.None ? node.Boundary : null,
            Offset = node.ContainerOffset != Pos.None ? node.ContainerOffset : null,
            Zoom = node.ContainerZoom != Models.Node.DefaultContainerZoom ? node.ContainerZoom : null,
            Color = node.Color,
        };
}

record LinkDto(string SourceName, string TargetName, NodeType TargetType) : IItem
{
    public static LinkDto From(Models.Link link) =>
        new(link.Source.Name, link.Target.Name, new NodeType(link.Target.Type.Text));
}

[Transient]
class PersistenceService(IFileService fileService) : IPersistenceService
{
    public Task<R> WriteAsync(ModelDto model)
    {
        return Task.Run(async () =>
        {
            var filePath = model.Path;
            using var t = Timing.Start($"Write model '{filePath}'");

            await fileService.WriteAsync(filePath, model);

            return R.Ok;
        });
    }

    public Task<R<ModelDto>> ReadAsync(string modelPath)
    {
        return Task.Run<R<ModelDto>>(async () =>
        {
            var filePath = modelPath;
            using var t = Timing.Start();

            if (!Try(out var model, out var e2, await fileService.ReadAsync<ModelDto>(filePath)))
                return e2;
            if (model.FormatVersion != ModelDto.CurrentVersion)
            {
                var error = R.Error(
                    $"Cached model format version {model.FormatVersion} != {ModelDto.CurrentVersion} (current)"
                );
                Log.Error(error.ErrorMessage);
                return error;
            }
            Log.Info("Read cached model", modelPath);
            return model;
        });
    }
}

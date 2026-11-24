using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Shared.CloudSync;
using Dependinator.UI.Shared.Types;
using Shared;

namespace Dependinator.UI.Tests.Shared;

public class CloudModelSerializerTests
{
    [Fact]
    public void CreateDocument_AndReadModel_ShouldRoundTripModelDto()
    {
        ModelDto modelDto = new()
        {
            Name = "/models/sample.model",
            Zoom = 1.5,
            Offset = new Pos(4, 8),
            ViewRect = new Rect(1, 2, 3, 4),
            Nodes = [],
            Links = [],
            Lines = [],
        };

        CloudModelDocument document = CloudModelSerializer.CreateDocument(@" \models\sample.model ", modelDto);

        Assert.Equal("/models/sample.model", document.NormalizedPath);
        Assert.Equal(CloudModelPath.CreateKey("/models/sample.model"), document.ModelKey);

        Result<ModelDto> result = CloudModelSerializer.ReadModel(document);

        var roundTrippedModel = AssertOk(result);
        Assert.Equal(modelDto.Name, roundTrippedModel.Name);
        Assert.Equal(modelDto.Zoom, roundTrippedModel.Zoom);
        Assert.Equal(modelDto.Offset, roundTrippedModel.Offset);
        Assert.Equal(modelDto.ViewRect, roundTrippedModel.ViewRect);
    }

    [Fact]
    public void GetContentHash_ShouldDifferWhenIncludeTestProjectsDiffers()
    {
        // The option must be part of the hash, otherwise flipping it would never mark the model
        // as changed and the new value would never sync.
        ModelDto dto1 = new()
        {
            Name = "test",
            Nodes = [],
            Links = [],
        };
        ModelDto dto2 = dto1 with { IncludeTestProjects = true };

        Assert.NotEqual(CloudModelSerializer.GetContentHash(dto1), CloudModelSerializer.GetContentHash(dto2));
    }

    [Fact]
    public void GetContentHash_ShouldIgnoreViewStateFields()
    {
        ModelDto dto1 = new()
        {
            Name = "test",
            Zoom = 1.5,
            Offset = new Pos(10, 20),
            ViewRect = new Rect(1, 2, 3, 4),
            Nodes = [],
            Links = [],
        };
        ModelDto dto2 = new()
        {
            Name = "test",
            Zoom = 3.0,
            Offset = new Pos(99, 99),
            ViewRect = new Rect(5, 6, 7, 8),
            Nodes = [],
            Links = [],
        };

        Assert.Equal(CloudModelSerializer.GetContentHash(dto1), CloudModelSerializer.GetContentHash(dto2));
    }

    [Fact]
    public void CreateDocument_HashShouldMatchGetContentHash()
    {
        ModelDto modelDto = new()
        {
            Name = "test",
            Zoom = 2.0,
            Offset = new Pos(5, 5),
            ViewRect = new Rect(1, 1, 10, 10),
            Nodes = [],
            Links = [],
        };

        CloudModelDocument document = CloudModelSerializer.CreateDocument("test", modelDto);

        Assert.Equal(CloudModelSerializer.GetContentHash(modelDto), document.ContentHash);
    }

    // The mark of the user's edits since the last sync is each device's own: it changes no
    // hash, a model without it (saved before the mark existed) hashes as it did before, and it
    // does not travel to the cloud.
    [Fact]
    public void GetContentHash_ShouldLeaveOutTheUserEditsMark()
    {
        ModelDto model = Model([Node("App"), Node("App.Core", parent: "App")]);

        string hash = CloudModelSerializer.GetContentHash(model);

        Assert.Equal(hash, CloudModelSerializer.GetContentHash(model with { HasUserEdits = true }));
        Assert.Equal(hash, CloudModelSerializer.GetContentHash(model with { HasUserEdits = false }));
        Assert.DoesNotContain("HasUserEdits", System.Text.Json.JsonSerializer.Serialize(model));
    }

    [Fact]
    public void CreateDocument_ShouldLeaveOutTheUserEditsMark()
    {
        ModelDto model = Model([Node("App")]) with { HasUserEdits = true };

        CloudModelDocument document = CloudModelSerializer.CreateDocument("/models/sample.model", model);

        Assert.Null(AssertOk(CloudModelSerializer.ReadModel(document)).HasUserEdits);
    }

    // The structure is what a parse makes: the same for the same source, whatever was laid out
    // or in which order the model was put together.
    [Fact]
    public void GetStructureHash_ShouldBeTheSame_WhateverTheLayoutAndTheOrder()
    {
        LinkDto link = new("App.Core", "App.Api", "Type");
        ModelDto model = Model([Node("App"), Node("App.Core", parent: "App"), Node("App.Api", parent: "App")], [link]);
        ModelDto laidOutOtherwise = Model(
            [
                Node("App.Api", parent: "App") with
                {
                    Boundary = new Rect(10, 20, 100, 100),
                    Color = "red",
                },
                Node("App") with
                {
                    Zoom = 0.5,
                    IsChildrenLayoutRequired = true,
                },
                Node("App.Core", parent: "App"),
            ],
            [link]
        ) with
        {
            Lines = [new LineDto { LineId = "1", SegmentPoints = [new Pos(1, 2)] }],
        };

        Assert.Equal(
            CloudModelSerializer.GetStructureHash(model),
            CloudModelSerializer.GetStructureHash(laidOutOtherwise)
        );
        Assert.NotEqual(
            CloudModelSerializer.GetContentHash(model),
            CloudModelSerializer.GetContentHash(laidOutOtherwise)
        );
    }

    [Fact]
    public void GetStructureHash_ShouldDiffer_WhenAParseMadeSomethingElse()
    {
        ModelDto model = Model([Node("App"), Node("App.Core", parent: "App")]);
        string hash = CloudModelSerializer.GetStructureHash(model);

        ModelDto moreNodes = Model([Node("App"), Node("App.Core", parent: "App"), Node("App.Api", parent: "App")]);
        ModelDto otherLine = Model([
            Node("App"),
            Node("App.Core", parent: "App") with
            {
                Properties = new() { FileSpan = new Dependinator.Core.Parsing.FileSpan("Core.cs", 3, 9) },
            },
        ]);
        ModelDto moreLinks = Model(model.Nodes, [new LinkDto("App.Core", "App", "Type")]);

        Assert.NotEqual(hash, CloudModelSerializer.GetStructureHash(moreNodes));
        Assert.NotEqual(hash, CloudModelSerializer.GetStructureHash(otherLine));
        Assert.NotEqual(hash, CloudModelSerializer.GetStructureHash(moreLinks));
    }

    static ModelDto Model(IReadOnlyList<NodeDto> nodes, IReadOnlyList<LinkDto>? links = null) =>
        new()
        {
            Name = "/models/sample.model",
            Nodes = nodes,
            Links = links ?? [],
        };

    static NodeDto Node(string name, string parent = "") =>
        new()
        {
            Name = name,
            ParentName = parent,
            Type = "Type",
        };
}

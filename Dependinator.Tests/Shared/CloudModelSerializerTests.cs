using Dependinator.Modeling.Dtos;
using Dependinator.Shared.CloudSync;
using Dependinator.Shared.Types;
using Shared;
using static Dependinator.Core.Utils.Result;

namespace Dependinator.Tests.Shared;

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

        R<ModelDto> result = CloudModelSerializer.ReadModel(document);

        Assert.True(Try(out var roundTrippedModel, out var error, result), error?.ErrorMessage);
        Assert.Equal(modelDto.Name, roundTrippedModel.Name);
        Assert.Equal(modelDto.Zoom, roundTrippedModel.Zoom);
        Assert.Equal(modelDto.Offset, roundTrippedModel.Offset);
        Assert.Equal(modelDto.ViewRect, roundTrippedModel.ViewRect);
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

        Assert.Equal(
            CloudModelSerializer.GetContentHash(dto1),
            CloudModelSerializer.GetContentHash(dto2)
        );
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
}

using Dependinator.Models;
using Dependinator.Shared;
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
}

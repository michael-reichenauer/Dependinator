using System.Text.Json;
using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling.Dtos;
using ModelNode = Dependinator.UI.Modeling.Models.Node;

namespace Dependinator.UI.Tests.Models;

public class NodeIconPersistenceTests
{
    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    static NodeDto MakeDto(string? iconName) =>
        new()
        {
            Name = "Node",
            ParentName = "Parent",
            Type = NodeType.ClassType.ToString(),
            IconName = iconName,
        };

    [Fact]
    public void Serialize_ShouldOmitIconName_WhenNull()
    {
        var json = JsonSerializer.Serialize(MakeDto(null), Options);

        Assert.DoesNotContain("IconName", json);
    }

    [Fact]
    public void Serialize_ShouldIncludeIconName_WhenSet()
    {
        var json = JsonSerializer.Serialize(MakeDto("Database"), Options);

        Assert.Contains("\"IconName\":\"Database\"", json);
    }

    [Fact]
    public void Deserialize_ShouldSucceed_WhenJsonHasUnknownMember()
    {
        // New optional fields must be forward/backward compatible without bumping the format
        // version: an unknown member on read is skipped rather than throwing.
        var json = """
            { "Name": "Node", "ParentName": "Parent", "Type": "ClassType", "SomeFutureField": 42 }
            """;

        var dto = JsonSerializer.Deserialize<NodeDto>(json, Options);

        Assert.NotNull(dto);
        Assert.Null(dto.IconName);
    }

    [Fact]
    public void ToDto_And_SetFromDto_ShouldRoundTripCustomIconName()
    {
        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var node = new ModelNode("Node", root) { Type = NodeType.ClassType, CustomIconName = "Database" };

        var dto = node.ToDto();
        Assert.Equal("Database", dto.IconName);

        var restored = new ModelNode("Node", root);
        restored.SetFromDto(dto);
        Assert.Equal("Database", restored.CustomIconName);
    }

    [Fact]
    public void SetFromDto_ShouldLeaveCustomIconNull_WhenDtoOmitsIt()
    {
        var root = new ModelNode("", null!) { Type = NodeType.Root };
        var node = new ModelNode("Node", root) { Type = NodeType.ClassType };

        node.SetFromDto(MakeDto(null));

        Assert.Null(node.CustomIconName);
    }
}

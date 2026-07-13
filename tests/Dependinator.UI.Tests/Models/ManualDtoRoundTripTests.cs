using System.Text.Json;
using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Tests.Models;

// Guards that IsManual round-trips through the persisted DTOs, and that parsed (non-manual)
// items keep an identical serialized form (so no format bump / cloud-hash churn is triggered).
public class ManualDtoRoundTripTests
{
    [Fact]
    public void NodeDto_ShouldRoundTripIsManual()
    {
        var root = new Node("", null!) { Type = Dependinator.Core.Parsing.NodeType.Root };
        var node = new Node("Manual", root) { IsManual = true };

        var restored = new Node("Manual", root);
        restored.SetFromDto(node.ToDto());

        Assert.True(node.ToDto().IsManual);
        Assert.True(restored.IsManual);
    }

    [Fact]
    public void LinkDto_ShouldRoundTripIsManual()
    {
        var root = new Node("", null!) { Type = Dependinator.Core.Parsing.NodeType.Root };
        var source = new Node("A", root);
        var target = new Node("B", root);
        var link = new Link(source, target) { IsManual = true };

        Assert.True(link.ToDto().IsManual);
    }

    [Fact]
    public void NodeDto_ShouldOmitIsManual_WhenFalse()
    {
        var json = JsonSerializer.Serialize(
            new NodeDto
            {
                Name = "Parsed",
                ParentName = "",
                Type = "Type",
            }
        );

        Assert.DoesNotContain("IsManual", json);
    }

    [Fact]
    public void NodeDto_ShouldRoundTripIsNoteAndDescription()
    {
        var root = new Node("", null!) { Type = Dependinator.Core.Parsing.NodeType.Root };
        var note = new Node("1", root) { IsManual = true, IsNote = true };
        note.SetDescription("Start here");

        var restored = new Node("1", root);
        restored.SetFromDto(note.ToDto());

        Assert.True(note.ToDto().IsNote);
        Assert.True(restored.IsNote);
        Assert.True(restored.IsManual);
        Assert.Equal("Start here", restored.Description);
    }

    [Fact]
    public void NodeDto_ShouldOmitIsNote_WhenFalse()
    {
        var json = JsonSerializer.Serialize(
            new NodeDto
            {
                Name = "Parsed",
                ParentName = "",
                Type = "Type",
            }
        );

        Assert.DoesNotContain("IsNote", json);
    }

    [Fact]
    public void LinkDto_ShouldOmitIsManual_WhenFalse_ButIncludeWhenTrue()
    {
        Assert.DoesNotContain("IsManual", JsonSerializer.Serialize(new LinkDto("A", "B", "None")));
        Assert.Contains("IsManual", JsonSerializer.Serialize(new LinkDto("A", "B", "None") { IsManual = true }));
    }
}

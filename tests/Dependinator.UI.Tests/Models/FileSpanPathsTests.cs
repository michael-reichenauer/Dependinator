using System.Text.Json;
using Dependinator.Core.Parsing;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Dtos;

namespace Dependinator.UI.Tests.Models;

// Guards that node FileSpans persist relative to the model (.sln) folder and are restored
// to absolute paths on load, so cached/synced models keep source locations across base paths.
public class FileSpanPathsTests
{
    const string ModelPath = "/workspaces/App/App.sln";

    static NodeDto Dto(FileSpan? fileSpan) =>
        new()
        {
            Name = "App*dll.Class1",
            ParentName = "App*dll",
            Type = "ClassType",
            Properties = new() { FileSpan = fileSpan },
        };

    [Fact]
    public void ToRelative_ShouldStripModelFolder()
    {
        var dto = Dto(new FileSpan("/workspaces/App/src/Class1.cs", 10, 20));

        var relative = FileSpanPaths.ToRelative(dto, ModelPath);

        Assert.Equal(new FileSpan("src/Class1.cs", 10, 20), relative.Properties.FileSpan);
    }

    [Fact]
    public void ToRelative_ShouldKeepPathsOutsideModelFolder()
    {
        var span = new FileSpan("/other/place/Class1.cs", 1, 2);

        var relative = FileSpanPaths.ToRelative(Dto(span), ModelPath);

        Assert.Equal(span, relative.Properties.FileSpan);
    }

    [Fact]
    public void ToRelative_ShouldNotMatchPartialFolderName()
    {
        // "/workspaces/App2" starts with "/workspaces/App" but is a different folder.
        var span = new FileSpan("/workspaces/App2/src/Class1.cs", 1, 2);

        var relative = FileSpanPaths.ToRelative(Dto(span), ModelPath);

        Assert.Equal(span, relative.Properties.FileSpan);
    }

    [Fact]
    public void ToAbsolute_ShouldPrependModelFolder()
    {
        var dto = Dto(new FileSpan("src/Class1.cs", 10, 20));

        var absolute = FileSpanPaths.ToAbsolute(dto, ModelPath);

        Assert.Equal(new FileSpan("/workspaces/App/src/Class1.cs", 10, 20), absolute.Properties.FileSpan);
    }

    [Fact]
    public void ToAbsolute_ShouldKeepAbsolutePaths()
    {
        var unixSpan = new FileSpan("/other/place/Class1.cs", 1, 2);
        var windowsSpan = new FileSpan(@"C:\other\Class1.cs", 1, 2);

        Assert.Equal(unixSpan, FileSpanPaths.ToAbsolute(Dto(unixSpan), ModelPath).Properties.FileSpan);
        Assert.Equal(windowsSpan, FileSpanPaths.ToAbsolute(Dto(windowsSpan), ModelPath).Properties.FileSpan);
    }

    [Fact]
    public void ToAbsolute_ShouldUseModelPathSeparator_OnWindows()
    {
        var dto = Dto(new FileSpan(@"src\Class1.cs", 10, 20));

        var absolute = FileSpanPaths.ToAbsolute(dto, @"C:\code\App\App.sln");

        Assert.Equal(new FileSpan(@"C:\code\App\src\Class1.cs", 10, 20), absolute.Properties.FileSpan);
    }

    [Fact]
    public void RoundTrip_ShouldRestoreAbsolutePath_ForDifferentBasePath()
    {
        var dto = Dto(new FileSpan("/workspaces/App/src/Class1.cs", 10, 20));

        var relative = FileSpanPaths.ToRelative(dto, ModelPath);
        var absolute = FileSpanPaths.ToAbsolute(relative, "/home/user/repos/App/App.sln");

        Assert.Equal(new FileSpan("/home/user/repos/App/src/Class1.cs", 10, 20), absolute.Properties.FileSpan);
    }

    [Fact]
    public void ConvertedDtos_ShouldNotChange_WhenFileSpanIsNull()
    {
        var dto = Dto(null);

        Assert.Same(dto, FileSpanPaths.ToRelative(dto, ModelPath));
        Assert.Same(dto, FileSpanPaths.ToAbsolute(dto, ModelPath));
    }

    [Fact]
    public void NodeDto_ShouldOmitFileSpan_WhenNull()
    {
        var json = JsonSerializer.Serialize(Dto(null));

        Assert.DoesNotContain("FileSpan", json);
    }

    [Fact]
    public void NodeDto_ShouldRoundTripFileSpan_ThroughJson()
    {
        var dto = Dto(new FileSpan("src/Class1.cs", 10, 20));

        var restored = JsonSerializer.Deserialize<NodeDto>(JsonSerializer.Serialize(dto));

        Assert.Equal(dto.Properties.FileSpan, restored!.Properties.FileSpan);
    }
}

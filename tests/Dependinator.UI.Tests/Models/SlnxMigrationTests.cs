using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Tests.Models;

// An .slnx opened for the first time takes over the saved model of its .sln, renamed.
public class SlnxMigrationTests
{
    [Theory]
    [InlineData("/repo/App.slnx", "/repo/App.sln")]
    [InlineData("C:\\repo\\App.SLNX", "C:\\repo\\App.SLN")]
    [InlineData("/repo/App.sln", null)]
    [InlineData("/repo/main.bicep", null)]
    public void SlnPath_ShouldBeTheSlnBesideAnSlnx(string path, string? expected)
    {
        Assert.Equal(expected, SlnxMigration.SlnPath(path));
    }

    [Fact]
    public void FromSlnModel_ShouldRenameTheRoot_AndKeepTheRest()
    {
        var model = AssertModel(SlnxMigration.FromSlnModel(SlnModel(), "/repo/App.slnx"));

        Assert.Equal("/repo/App.slnx", model.Name);
        Assert.Equal(
            [
                ("App*slnx", ""),
                ("Core", "App*slnx"),
                ("Core.Type", "Core"),
                ("App*slnx.Note", "App*slnx"),
                ("Externals", ""),
            ],
            model.Nodes.Select(n => (n.Name, n.ParentName))
        );
        Assert.Equal("The core", model.Nodes.Single(n => n.Name == "Core").Alias);
        Assert.Equal(new Rect(20, 20, 100, 100), model.Nodes.Single(n => n.Name == "Core").Boundary);
        Assert.Equal([("App*slnx.Note", "Core.Type")], model.Links.Select(l => (l.SourceName, l.TargetName)));
        Assert.Equal([("App*slnx", "Externals")], model.Rules.Select(r => (r.From, r.To)));
        Assert.Equal(["App*slnx", "Core"], Assert.Single(model.KindRules).Layers);
        Assert.Equal("App*slnx", Assert.Single(model.AcceptedCycles).Container);
        Assert.Equal(1.5, model.Zoom);
    }

    // Line ids are hashes of the names of their ends
    [Fact]
    public void FromSlnModel_ShouldGiveTheLinesOfTheRootTheirNewIds()
    {
        var model = AssertModel(SlnxMigration.FromSlnModel(SlnModel(), "/repo/App.slnx"));

        Assert.Equal(
            [
                LineId.From("App*slnx", "Externals").Value,
                LineId.FromDirect("App*slnx.Note", "Core.Type").Value,
                LineId.From("Core", "Externals").Value,
            ],
            model.Lines.Select(l => l.LineId)
        );
        Assert.Equal(("App*slnx", "Externals"), (model.Lines[0].Source, model.Lines[0].Target));
        Assert.Equal([new Pos(1, 2)], model.Lines[0].SegmentPoints);
    }

    [Fact]
    public void FromSlnModel_ShouldBeNull_ForAModelWithoutOneSolutionRoot()
    {
        var model = SlnModel() with { Nodes = [Node("main*bicep", "", "Solution")] };

        Assert.Null(SlnxMigration.FromSlnModel(model, "/repo/App.slnx"));
    }

    static ModelDto AssertModel(ModelDto? model) => Assert.IsType<ModelDto>(model);

    static NodeDto Node(string name, string parent, string type = "ClassType") =>
        new()
        {
            Name = name,
            ParentName = parent,
            Type = type,
        };

    static ModelDto SlnModel() =>
        new()
        {
            Name = "/repo/App.sln",
            Zoom = 1.5,
            Nodes =
            [
                Node("App*sln", "", "Solution"),
                Node("Core", "App*sln") with
                {
                    Alias = "The core",
                    Boundary = new Rect(20, 20, 100, 100),
                },
                Node("Core.Type", "Core"),
                Node("App*sln.Note", "App*sln", "None") with
                {
                    IsManual = true,
                    IsNote = true,
                },
                Node("Externals", "", "Externals"),
            ],
            Links = [new LinkDto("App*sln.Note", "Core.Type", "ClassType") { IsManual = true }],
            Lines =
            [
                new LineDto
                {
                    LineId = LineId.From("App*sln", "Externals").Value,
                    SegmentPoints = [new Pos(1, 2)],
                    Label = "Uses",
                    Source = "App*sln",
                    Target = "Externals",
                },
                new LineDto { LineId = LineId.FromDirect("App*sln.Note", "Core.Type").Value, SegmentPoints = [] },
                new LineDto { LineId = LineId.From("Core", "Externals").Value, SegmentPoints = [] },
            ],
            Rules = [new RuleDto { From = "App*sln", To = "Externals" }],
            KindRules =
            [
                new RuleDto
                {
                    From = "App*sln",
                    To = "Core",
                    Layers = ["App*sln", "Core"],
                },
            ],
            AcceptedCycles = [new AcceptedCycleDto { Container = "App*sln", Members = ["Core"] }],
        };
}

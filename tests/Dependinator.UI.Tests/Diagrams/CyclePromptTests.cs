using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared;
using Dependinator.UI.Shared.Types;
using FileSpan = Dependinator.Core.Parsing.FileSpan;
using Node = Dependinator.UI.Modeling.Models.Node;
using NodeType = Dependinator.Core.Parsing.NodeType;
using Parsing = Dependinator.Core.Parsing;

namespace Dependinator.UI.Tests.Diagrams;

// The prompt that asks an AI about a cycle: the members with their descriptions, the links per
// direction (per member that makes them, or per type when there are many), the source behind the
// direction with the fewest links when the files can be read, and the answer format.
public class CyclePromptTests
{
    readonly ModelMgr modelMgr = new(new StateMgr());

    static Node AddNode(IModel model, string name, Node parent, NodeType type, string? description = null)
    {
        var node = new Node(name, parent) { Boundary = new Rect(0, 0, 100, 100), Type = type };
        node.SetDescription(description);
        parent.AddChild(node);
        model.TryAddNode(node);
        return node;
    }

    static void AddLink(IModel model, Node source, Node target)
    {
        var link = new Link(source, target);
        model.TryAddLink(link);
        source.AddSourceLink(link);
        target.AddTargetLink(link);
        new LineService().AddLinesFromSourceToTarget(model, link);
    }

    static void SetSpan(Node node, string path, int startLine, int endLine) =>
        node.Update(
            new Parsing.Node(
                node.Name,
                new Parsing.NodeProperties { FileSpan = new FileSpan(path, startLine, endLine) }
            )
        );

    // Parsing (three types using Utils.Log) and Utils (an extension using Parsing.NodeType once).
    Cycle AddParsingUtilsCycle()
    {
        using var model = modelMgr.UseModel();
        model.Path = "/repo/App.sln";
        var core = AddNode(model, "Core", model.Root, NodeType.Namespace);
        var parsing = AddNode(model, "Core.Parsing", core, NodeType.Namespace);
        var utils = AddNode(model, "Core.Utils", core, NodeType.Namespace);
        var service = AddNode(model, "Core.Parsing.Service", parsing, NodeType.ClassType);
        var nodeType = AddNode(model, "Core.Parsing.NodeType", parsing, NodeType.EnumType);
        var log = AddNode(model, "Core.Utils.Log", utils, NodeType.ClassType);
        var extensions = AddNode(model, "Core.Utils.Ext", utils, NodeType.ClassType);
        var isMember = AddNode(model, "Core.Utils.Ext.IsMember", extensions, NodeType.PropertyMember);
        SetSpan(service, "/repo/src/Parsing/Service.cs", 1, 50);
        SetSpan(extensions, "/repo/src/Utils/Ext.cs", 5, 20);
        SetSpan(isMember, "/repo/src/Utils/Ext.cs", 9, 9);
        AddLink(model, AddNode(model, "Core.Parsing.Service.A()", service, NodeType.MethodMember), log);
        AddLink(model, AddNode(model, "Core.Parsing.Reader", parsing, NodeType.ClassType), log);
        AddLink(model, AddNode(model, "Core.Parsing.Writer", parsing, NodeType.ClassType), log);
        AddLink(model, isMember, nodeType);
        return CycleService.Compute(model).Cycles.Single();
    }

    static string PromptForTheOnlyCycle(IModel model) =>
        CyclePrompt.Build(model, CycleService.Compute(model).Cycles.Single()).ReplaceLineEndings("\n");

    [Fact]
    public async Task Build_ShouldDescribeTheCycle_ItsLinks_AndTheAnswerFormat()
    {
        // The model lock is a monitor, so it is released before awaiting.
        string prompt;
        using (var model = modelMgr.UseModel())
        {
            var solution = AddNode(model, "Demo*sln", model.Root, NodeType.Solution, "Demo visualizes code.");
            var assembly = AddNode(model, "Demo*Core*dll", solution, NodeType.Assembly);
            var utils = AddNode(model, "Demo*Core*dll.Demo.Core.Utils", assembly, NodeType.Namespace);
            var result = AddNode(
                model,
                "Demo*Core*dll.Demo.Core.Utils.Result",
                utils,
                NodeType.StructType,
                "The result of an operation that either succeeds or fails:\nSuccess or Error"
            );
            var resultT = AddNode(
                model,
                "Demo*Core*dll.Demo.Core.Utils.Result<T>",
                utils,
                NodeType.StructType,
                "The result of an operation that either produces a value or fails: T or Error"
            );
            var ok = AddNode(model, "Demo*Core*dll.Demo.Core.Utils.Result.Ok", result, NodeType.PropertyMember);
            var constructor = AddNode(
                model,
                "Demo*Core*dll.Demo.Core.Utils.Result.Result(Demo.Core.Utils.Error)",
                result,
                NodeType.MethodMember // As the C# parser types constructors
            );
            var catchT = AddNode(
                model,
                "Demo*Core*dll.Demo.Core.Utils.Result.Catch<T>(System.Func<T>)",
                result,
                NodeType.MethodMember,
                "Runs a function that reports failure by throwing and returns its value, or the exception as an error."
            );
            var toResult = AddNode(
                model,
                "Demo*Core*dll.Demo.Core.Utils.Result<T>.ToResult()",
                resultT,
                NodeType.MethodMember,
                "Dropping the value keeps the outcome"
            );
            AddLink(model, catchT, resultT);
            AddLink(model, toResult, result);
            AddLink(model, toResult, constructor);
            AddLink(model, toResult, ok);

            prompt = PromptForTheOnlyCycle(model);
        }

        await Verify(prompt);
    }

    [Fact]
    public void Build_ShouldFoldAccessorsIntoTheirMember_AndGroupTargetMembersUnderTheirType()
    {
        using var model = modelMgr.UseModel();
        var core = AddNode(model, "Core", model.Root, NodeType.Namespace);
        var parsing = AddNode(model, "Core.Parsing", core, NodeType.Namespace);
        var utils = AddNode(model, "Core.Utils", core, NodeType.Namespace);
        var nodeType = AddNode(model, "Core.Parsing.NodeType", parsing, NodeType.EnumType);
        var field = AddNode(model, "Core.Parsing.NodeType.FieldMember", nodeType, NodeType.FieldMember);
        var method = AddNode(model, "Core.Parsing.NodeType.MethodMember", nodeType, NodeType.FieldMember);
        var service = AddNode(model, "Core.Parsing.Service", parsing, NodeType.ClassType);
        var log = AddNode(model, "Core.Utils.Log", utils, NodeType.ClassType);
        var extensions = AddNode(model, "Core.Utils.Extensions", utils, NodeType.ClassType);
        var isMember = AddNode(model, "Core.Utils.Extensions.IsMember", extensions, NodeType.PropertyMember);
        var getter = AddNode(model, "Core.Utils.Extensions.IsMember.get", isMember, NodeType.MethodMember);
        AddLink(model, service, log);
        AddLink(model, isMember, nodeType);
        AddLink(model, getter, field);
        AddLink(model, getter, method);

        var prompt = PromptForTheOnlyCycle(model);

        Assert.Contains("In namespace Core, these siblings depend on each other in a circle:", prompt);
        Assert.Contains("Parsing → Utils (1 link)\n- Parsing.Service → Utils.Log\n", prompt);
        Assert.Contains(
            "Utils → Parsing (3 links)\n- Utils.Extensions.IsMember → Parsing.NodeType (FieldMember, MethodMember)\n",
            prompt
        );
    }

    [Fact]
    public void Build_ShouldShortenNames_MergeMembersThatReadTheSame_AndClipAtASentence()
    {
        var firstSentence = "First " + new string('x', 190) + ".";
        using var model = modelMgr.UseModel();
        var container = AddNode(model, "C", model.Root, NodeType.Namespace);
        var a = AddNode(model, "C.A", container, NodeType.Namespace, firstSentence + " Second " + new string('y', 150));
        var b = AddNode(model, "C.B", container, NodeType.Namespace);
        var item = AddNode(model, "C.A.Item", a, NodeType.ClassType);
        var extensions = AddNode(model, "C.A.Ext", a, NodeType.ClassType);
        // Two members whose display names are the same, as for C# extension block members.
        var isA = AddNode(model, "C.A.Ext.extension(global::C.B.T).IsA", extensions, NodeType.PropertyMember);
        var isB = AddNode(model, "C.A.Ext.extension(global::C.B.T).IsB", extensions, NodeType.PropertyMember);
        var result = AddNode(model, "C.B.Result<global::C.A.Item>", b, NodeType.StructType);
        var constructor = AddNode(
            model,
            "C.B.Result<global::C.A.Item>.Result(string)",
            result,
            NodeType.ConstructorMember
        );
        var ok = AddNode(model, "C.B.Result<global::C.A.Item>.Ok", result, NodeType.PropertyMember);
        AddLink(model, isA, constructor);
        AddLink(model, isB, ok);
        AddLink(model, result, item);

        var prompt = PromptForTheOnlyCycle(model);

        Assert.Contains($"- A (namespace): \"{firstSentence}\"\n", prompt);
        Assert.Contains("A → B (2 links)\n- A.Ext.extension(T) → B.Result<Item> (constructor, Ok)\n\n", prompt);
        Assert.Contains("B → A (1 link)\n- B.Result<Item> → A.Item\n", prompt);
    }

    [Fact]
    public void Build_ShouldListLinksPerType_AndCapThem_WhenManyMembersMakeThem()
    {
        using var model = modelMgr.UseModel();
        var container = AddNode(model, "C", model.Root, NodeType.Namespace);
        var a = AddNode(model, "C.A", container, NodeType.Namespace);
        var b = AddNode(model, "C.B", container, NodeType.Namespace);
        var target = AddNode(model, "C.B.Target", b, NodeType.ClassType);
        AddLink(model, target, a);
        for (var i = 0; i < 35; i++)
        {
            var type = AddNode(model, $"C.A.Type{i:00}", a, NodeType.ClassType);
            AddLink(model, AddNode(model, $"C.A.Type{i:00}.Use", type, NodeType.MethodMember), target);
        }

        var prompt = PromptForTheOnlyCycle(model);

        Assert.Contains("A → B (35 links)\n- A.Type00 → B.Target\n", prompt);
        Assert.DoesNotContain("A.Type00.Use", prompt);
        Assert.Contains("- A.Type29 → B.Target\n- … and 5 more\n", prompt);
        Assert.DoesNotContain("A.Type30", prompt);
    }

    [Fact]
    public void Build_ShouldSay_WhenPartOfTheCycleWasAcceptedBefore()
    {
        using var model = modelMgr.UseModel();
        var container = AddNode(model, "C", model.Root, NodeType.Namespace);
        var a = AddNode(model, "C.A", container, NodeType.ClassType);
        var b = AddNode(model, "C.B", container, NodeType.ClassType);
        AddLink(model, a, b);
        AddLink(model, b, a);
        var cycle = CycleService.Compute(model).Cycles.Single() with
        {
            AcceptedBefore = new AcceptedCycle("C", ["C.A"], "A and B are one concept"),
        };

        var prompt = CyclePrompt.Build(model, cycle).ReplaceLineEndings("\n");

        Assert.Contains(
            "Part of it was accepted before, with the reason \"A and B are one concept\", but B joined the cycle since.",
            prompt
        );
    }

    [Fact]
    public void GetSourceRequests_ShouldPickTheDirectionsMadeByOneOrTwoTypes()
    {
        var cycle = AddParsingUtilsCycle();
        using var model = modelMgr.UseModel();

        var requests = CyclePrompt.GetSourceRequests(model, cycle);

        // Parsing → Utils is made by three types.
        var request = Assert.Single(requests);
        Assert.Equal("Utils → Parsing", request.Direction);
        var unit = Assert.Single(request.Units);
        Assert.Equal(new FileSpan("/repo/src/Utils/Ext.cs", 5, 20), unit.Span);
        Assert.Equal([9], unit.MemberLines);
    }

    [Fact]
    public void GetSourceRequests_ShouldFindTheTypeOfAMemberFiledUnderMadeUpNodes()
    {
        Cycle cycle;
        using (var model = modelMgr.UseModel())
        {
            var container = AddNode(model, "C", model.Root, NodeType.Namespace);
            var a = AddNode(model, "C.A", container, NodeType.StructType);
            var b = AddNode(model, "C.B", container, NodeType.StructType);
            SetSpan(b, "/repo/B.cs", 10, 40);
            // "implicit operator C.A(C.B)" is named with dots, so a parent is made up for "C".
            var madeUp = AddNode(model, "C.B.implicitoperatorC", b, NodeType.Parent);
            var conversion = AddNode(model, "C.B.implicitoperatorC.A(C.B)", madeUp, NodeType.MethodMember);
            SetSpan(conversion, "/repo/B.cs", 20, 20);
            AddLink(model, conversion, a);
            AddLink(model, a, b);
            cycle = CycleService.Compute(model).Cycles.Single();
        }
        using var reading = modelMgr.UseModel();

        var requests = CyclePrompt.GetSourceRequests(reading, cycle);

        var unit = Assert.Single(requests.Where(r => r.Direction == "B → A")).Units.Single();
        Assert.Equal(new FileSpan("/repo/B.cs", 10, 40), unit.Span);
        Assert.Equal([20], unit.MemberLines);
    }

    [Fact]
    public void Build_ShouldShowTheSource_WithPathsRelativeToTheModel()
    {
        var cycle = AddParsingUtilsCycle();
        using var model = modelMgr.UseModel();
        SourceSection[] sources =
        [
            new("Utils → Parsing", [new SourceExcerpt("/repo/src/Utils/Ext.cs", 5, ["class Ext", "{", "}"])]),
        ];

        var prompt = CyclePrompt.Build(model, cycle, sources).ReplaceLineEndings("\n");

        Assert.Contains(
            "## Source\n\nThe code behind the directions made by only one or two types, the ones most likely to be "
                + "broken or to be explained.\n\n### Utils → Parsing\n\nsrc/Utils/Ext.cs, lines 5–7:\n\n"
                + "```csharp\nclass Ext\n{\n}\n```\n\n## Your task",
            prompt
        );
    }

    [Fact]
    public void BuildFix_ShouldAskToMakeTheSuggestedChange_CheckingTheAssumptionsFirst()
    {
        var cycle = AddParsingUtilsCycle();
        using var model = modelMgr.UseModel();
        var suggestion = new CycleSuggestion(
            false,
            "One misplaced class.",
            "UI only aliases Parsing.",
            "",
            "Move Ext into Parsing."
        );

        var prompt = CyclePrompt.BuildFix(model, cycle, suggestion).ReplaceLineEndings("\n");

        Assert.StartsWith("Please break a circular dependency in this codebase.", prompt);
        Assert.Contains("Utils → Parsing (1 link)\n- Utils.Ext.IsMember → Parsing.NodeType\n", prompt);
        Assert.Contains(
            "## The suggested change\n\nMove Ext into Parsing.\n\nWhy: One misplaced class.\n\n"
                + "Assumptions to check first: UI only aliases Parsing.\n",
            prompt
        );
        Assert.Contains(
            "## Where to look\n\nThe code behind the directions made by only one or two types:\n- src/Utils/Ext.cs\n",
            prompt
        );
        Assert.Contains("## Your task\n\n1. Check the assumptions above in the code first.", prompt);
        Assert.DoesNotContain("## Source", prompt);
    }

    [Fact]
    public async Task GetPromptAsync_ShouldShowTheSourceItCanRead_AndLeaveOutWhatItCannot()
    {
        var cycle = AddParsingUtilsCycle();
        var files = new Mock<IWorkspaceFileService>();
        files
            .Setup(f => f.ReadSourceFileAsync("/repo/src/Utils/Ext.cs"))
            .ReturnsAsync((Result<string>)"// Kept in Utils on purpose\nclass Ext\n{\n}\n");
        var service = new CyclePromptService(modelMgr, files.Object);

        var prompt = await service.GetPromptAsync(cycle);

        Assert.Contains("```csharp\n// Kept in Utils on purpose\nclass Ext\n", prompt.ReplaceLineEndings("\n"));

        files.Setup(f => f.ReadSourceFileAsync(It.IsAny<string>())).ReturnsAsync((Result<string>)new Error("No file"));

        Assert.DoesNotContain("## Source", await service.GetPromptAsync(cycle));
    }
}

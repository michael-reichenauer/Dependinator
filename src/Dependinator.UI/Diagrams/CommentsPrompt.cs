using System.Text;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using static Dependinator.UI.Diagrams.PromptParts;
using NodeType = Dependinator.Core.Parsing.NodeType;

namespace Dependinator.UI.Diagrams;

// The text that asks an AI that can change the code (Claude Code) to add or improve the comments of
// some namespaces and types (or Bicep modules and resources), which Dependinator shows as their
// descriptions. Each one is listed with where it is declared, its comment now, and the parts
// outside it that it uses and that use it, which a good comment puts it among. It says where a
// comment must go for the parser to read it: right above the declaration, and for a namespace in
// one file only, since the parser shows the comments above every declaration of it.
static class CommentsPrompt
{
    // More than this in one run may not finish in time; the dialog warns.
    public const int ManyTargets = 150;

    const int MaxTargets = 400;
    const int MaxLinkedUnits = 5;

    // The nodes whose comments to improve: the given ones, and with isBelow everything inside
    // them, that the parser reads a comment for (namespaces and types with a place in the code,
    // Bicep modules and resources), outermost first.
    public static IReadOnlyList<Node> Targets(IEnumerable<Node> nodes, bool isBelow) =>
        [
            .. nodes
                .SelectMany(n => isBelow ? n.DescendantsAndSelfPreOrder() : [n])
                .Where(IsCommentable)
                .Distinct()
                .OrderBy(n => n.Name, StringComparer.Ordinal),
        ];

    public static bool IsCommentable(Node node) =>
        !node.IsManual
        && node.FileSpan is not null
        && (node.Type.IsType || node.Type is NodeType.Namespace or NodeType.Module or NodeType.Resource);

    public static string Build(IModel model, IReadOnlyList<Node> targets)
    {
        if (targets.Count == 0)
            return "";

        var isBicep = targets.Any(IsBicep);
        var isCSharp = targets.Any(t => !IsBicep(t));
        var parts =
            isCSharp && isBicep ? "namespaces, types, modules and resources"
            : isBicep ? "modules and resources"
            : "namespaces and types";

        var text = new StringBuilder();
        text.AppendLine(
            $"Please add or improve the code comments of the {parts} listed below. Dependinator, a tool that maps "
                + "the structure and dependencies of this codebase as a diagram, shows these comments as their "
                + "descriptions, so that a developer can see what each part is for without opening the code."
        );
        if (Codebase(targets[0]) is { } codebase)
            text.AppendLine().AppendLine(codebase);

        text.AppendLine()
            .Append(isCSharp ? GoodComment : GoodComment.Replace("namespace or type", "module or resource"));
        text.AppendLine().AppendLine("## Where a comment goes").AppendLine();
        if (isCSharp)
            text.Append(CSharpPlaces);
        if (isBicep)
            text.Append(BicepPlaces);
        text.AppendLine().Append(Rules);

        text.AppendLine().AppendLine($"## The {(targets.Count == 1 ? "one" : targets.Count)} to comment").AppendLine();
        foreach (var target in targets.Take(MaxTargets))
            AppendTarget(text, model, target);
        if (targets.Count > MaxTargets)
            text.AppendLine($"(… and {targets.Count - MaxTargets} more, left for another run.)").AppendLine();

        text.Append(Done);
        return text.ToString();
    }

    const string GoodComment = """
        ## What a good comment says

        - What the namespace or type is for, and the role it plays among the parts that use it and that it uses. Not a restatement of its name.
        - Why it is there or built this way, when that is not obvious: a decision, a constraint, a trap.
        - Briefly: one to three sentences for a type, two to four for a namespace. Plain words, no marketing.
        - True to the code: read the code before you write. Keep what an existing comment says that is still right and useful, and correct what is wrong or out of date.

        """;

    const string CSharpPlaces = """
        - A type's comment goes right above its declaration (above its attributes), in the style the file or the codebase already uses: `//` lines or a `///` summary.
        - A namespace's comment goes right above its `namespace` line, with no blank line between them, and only in the file named for it below. Dependinator shows the comments above every declaration of a namespace, so do not add it to other files, and leave a comment in another file alone unless it describes the whole namespace (then move it into the named file). A comment with a blank line below it is read as a file header, not a description.

        """;

    const string BicepPlaces = """
        - A Bicep module's or resource's comment is its `@description('...')` decorator, right above its declaration.

        """;

    const string Rules = """
        ## Rules

        - Change comments only: no code, no formatting, no renames, and no comments on members.
        - Lines like `-> Some.Target: text` in a comment describe a dependency line in the diagram: keep them, below the description.
        - Leave a comment that is already good as it is.
        - If you cannot tell what something is for, write what you can tell from the code, and say in your summary which ones need a closer look.

        """;

    const string Done = """
        ## When you are done

        Summarize what you did: how many comments you added and rewrote, which you left as they were and why, and which need a closer look. List every file you changed.

        """;

    static void AppendTarget(StringBuilder text, IModel model, Node target)
    {
        var span = target.FileSpan!;
        text.AppendLine($"### {Kind(target.Type)} {target.LongName}");
        var place = target.Type == NodeType.Namespace ? "Its comment goes in" : "Declared in";
        text.AppendLine($"{place} {DisplayPath(model, span.Path)}, line {span.StartLine + 1}");
        text.AppendLine(
            target.Description is { } description ? $"Comment now: \"{Clip(description)}\"" : "Comment now: none"
        );
        if (Names(NodePrompt.Uses(target)) is { } uses)
            text.AppendLine($"Uses: {uses}");
        if (Names(NodePrompt.UsedBy(target)) is { } usedBy)
            text.AppendLine($"Used by: {usedBy}");
        text.AppendLine();
    }

    static string? Names(IReadOnlyList<(Node Unit, int Links)> units)
    {
        if (units.Count == 0)
            return null;
        var names = string.Join(", ", units.Take(MaxLinkedUnits).Select(u => u.Unit.LongName));
        return units.Count > MaxLinkedUnits ? $"{names} and {units.Count - MaxLinkedUnits} more" : names;
    }

    static bool IsBicep(Node node) => node.Type is NodeType.Module or NodeType.Resource;
}

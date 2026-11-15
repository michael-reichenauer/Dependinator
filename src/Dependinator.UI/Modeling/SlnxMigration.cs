using Dependinator.Core.Shared;
using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling;

// A solution migrated to the .slnx format (dotnet sln migrate, Visual Studio's Save As .slnx)
// would be a new model: its path is another, and so is its root's name ("App*slnx" for
// "App*sln"). The first time an .slnx model is opened, the saved model of the .sln of the same
// name in its folder is taken over instead, its root renamed, so the user keeps what they did in
// it: the layout, notes, rules, accepted cycles and the view. A C# project's nodes are not named
// by their solution, so only the root, the names it prefixes (a note in it) and the lines to and
// from it change.
static class SlnxMigration
{
    // The .sln an .slnx model can take its layout from, or null for any other model.
    public static string? SlnPath(string path) => ModelPaths.IsSlnx(path) ? path[..^1] : null;

    // The .sln's saved model as the model of the .slnx at slnxPath, or null when the .sln's model
    // does not have exactly one solution root.
    public static ModelDto? FromSlnModel(ModelDto slnModel, string slnxPath)
    {
        var roots = slnModel
            .Nodes.Where(n =>
                n.ParentName == ""
                && n.Type == nameof(Parsing.NodeType.Solution)
                && n.Name.EndsWith("*sln", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();
        if (roots.Count != 1)
            return null;

        // Named as the Roslyn parser names a solution's root: its file name, "." written "*"
        var names = new RootRename(roots[0].Name, ModelPaths.DisplayName(slnxPath).Replace('.', '*'));
        var lineIds = names.LineIds(slnModel);

        return slnModel with
        {
            Name = slnxPath,
            Nodes =
            [
                .. slnModel.Nodes.Select(n => n with { Name = names.Of(n.Name), ParentName = names.Of(n.ParentName) }),
            ],
            Links =
            [
                .. slnModel.Links.Select(l =>
                    l with
                    {
                        SourceName = names.Of(l.SourceName),
                        TargetName = names.Of(l.TargetName),
                    }
                ),
            ],
            Lines =
            [
                .. slnModel.Lines.Select(l =>
                    l with
                    {
                        LineId = lineIds.GetValueOrDefault(l.LineId, l.LineId),
                        Source = names.OfNullable(l.Source),
                        Target = names.OfNullable(l.Target),
                    }
                ),
            ],
            Rules = [.. slnModel.Rules.Select(names.Of)],
            DismissedRules = [.. slnModel.DismissedRules.Select(names.Of)],
            KindRules = [.. slnModel.KindRules.Select(names.Of)],
            KindDismissedRules = [.. slnModel.KindDismissedRules.Select(names.Of)],
            AllowedViolations = [.. slnModel.AllowedViolations.Select(names.Of)],
            KindAllowedViolations = [.. slnModel.KindAllowedViolations.Select(names.Of)],
            AcceptedCycles =
            [
                .. slnModel.AcceptedCycles.Select(c =>
                    c with
                    {
                        Container = names.Of(c.Container),
                        Members = [.. c.Members.Select(names.Of)],
                    }
                ),
            ],
            ParseProblems = [.. slnModel.ParseProblems.Select(p => p with { NodeName = names.OfNullable(p.NodeName) })],
        };
    }

    sealed record RootRename(string From, string To)
    {
        public string Of(string name) =>
            name == From ? To
            : name.StartsWith(From + ".", StringComparison.Ordinal) ? To + name[From.Length..]
            : name;

        public string? OfNullable(string? name) => name is null ? null : Of(name);

        public RuleDto Of(RuleDto rule) =>
            rule with
            {
                From = Of(rule.From),
                To = Of(rule.To),
                Layers = rule.Layers is { } layers ? [.. layers.Select(Of)] : null,
            };

        public AllowedViolationDto Of(AllowedViolationDto violation) =>
            violation with
            {
                From = Of(violation.From),
                To = Of(violation.To),
                Source = Of(violation.Source),
                Target = Of(violation.Target),
                Layers = violation.Layers is { } layers ? [.. layers.Select(Of)] : null,
            };

        // Line ids are hashes of their ends' names: the new id of each saved line that ends at a
        // renamed node, by its old id. A line runs between siblings, from a container's edge to
        // its child, or straight between two nodes a link joins (a direct line), so only those
        // pairs are hashed: hashing a renamed node with every node took seconds in the browser.
        public IReadOnlyDictionary<string, string> LineIds(ModelDto model)
        {
            Dictionary<string, string> ids = [];
            if (model.Lines.Count == 0)
                return ids;

            var children = model.Nodes.ToLookup(n => n.ParentName, n => n.Name, StringComparer.Ordinal);
            foreach (var node in model.Nodes.Where(n => Of(n.Name) != n.Name))
            {
                var linked = model
                    .Links.Where(l => l.SourceName == node.Name || l.TargetName == node.Name)
                    .SelectMany(l => new[] { l.SourceName, l.TargetName });
                var ends = children[node.ParentName].Concat(children[node.Name]).Concat(linked).Distinct();
                foreach (var other in ends)
                {
                    Add(ids, node.Name, other);
                    Add(ids, other, node.Name);
                }
            }
            return ids;
        }

        void Add(Dictionary<string, string> ids, string source, string target)
        {
            ids[LineId.From(source, target).Value] = LineId.From(Of(source), Of(target)).Value;
            ids[LineId.FromDirect(source, target).Value] = LineId.FromDirect(Of(source), Of(target)).Value;
            ids[LineId.FromInheritance(source, target).Value] = LineId.FromInheritance(Of(source), Of(target)).Value;
        }
    }
}

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

    // The file name of a path with either kind of separator: the UI runs in the browser too,
    // where Path.GetFileName does not split a Windows path.
    public static string FileName(string path) => path[(path.LastIndexOfAny(['/', '\\']) + 1)..];

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
        var names = new RootRename(roots[0].Name, FileName(slnxPath).Replace('.', '*'));
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
        // renamed node, by its old id. Every pair of a renamed node and any node is tried, but only
        // a pair that names a saved line is hashed again under the new names.
        public IReadOnlyDictionary<string, string> LineIds(ModelDto model)
        {
            Dictionary<string, string> ids = [];
            var saved = model.Lines.Select(l => l.LineId).ToHashSet(StringComparer.Ordinal);
            if (saved.Count == 0)
                return ids;

            var names = model.Nodes.Select(n => n.Name).Distinct().ToList();
            foreach (var end in names.Where(name => Of(name) != name))
            {
                foreach (var other in names)
                {
                    Add(ids, saved, end, other);
                    Add(ids, saved, other, end);
                }
            }
            return ids;
        }

        void Add(Dictionary<string, string> ids, HashSet<string> saved, string source, string target)
        {
            if (saved.Contains(LineId.From(source, target).Value))
                ids[LineId.From(source, target).Value] = LineId.From(Of(source), Of(target)).Value;
            if (saved.Contains(LineId.FromDirect(source, target).Value))
                ids[LineId.FromDirect(source, target).Value] = LineId.FromDirect(Of(source), Of(target)).Value;
            if (saved.Contains(LineId.FromInheritance(source, target).Value))
                ids[LineId.FromInheritance(source, target).Value] = LineId
                    .FromInheritance(Of(source), Of(target))
                    .Value;
        }
    }
}

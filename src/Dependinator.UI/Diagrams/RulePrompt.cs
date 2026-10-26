using System.Text;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using static Dependinator.UI.Diagrams.PromptParts;

namespace Dependinator.UI.Diagrams;

// The text that asks an AI whether a dependency that breaks an architecture rule should be fixed
// (and how) or allowed as an exception, with a reason to store. Like CyclePrompt: the rule and the
// dependency with their descriptions, the concrete links (per member that makes them), and where
// the host can read the files, the source of the type that makes them, since a fix that looks
// right from the names alone may not compile. The other dependencies already allowed under the
// rule are listed with their reasons: they tell what the user considers fine.
static class RulePrompt
{
    const int MaxReasonLength = 200;
    const int MaxAllowedListed = 10;

    // The code to show with the prompt: the type (or module, resource) that makes the dependency.
    public static IReadOnlyList<SourceRequest> GetSourceRequests(IModel model, RuleViolation violation)
    {
        if (Ends(model, violation) is not var (source, target))
            return [];
        List<SourceUnit> units =
        [
            .. LinksOf(source, target).GroupBy(l => Unit(l.Source)).Where(g => HasSource(g.Key)).Select(SourceOf),
        ];
        return units.Count > 0 ? [new SourceRequest($"{source.LongName} → {target.LongName}", units)] : [];
    }

    public static string Build(
        IModel model,
        ArchitectureRule rule,
        RuleViolation violation,
        IReadOnlyList<SourceSection>? sources = null
    )
    {
        if (Ends(model, violation) is not var (source, target))
            return "";

        var text = new StringBuilder();
        text.AppendLine(
            "I use Dependinator, a tool that maps the structure and dependencies of a codebase. I declared an "
                + "architecture rule, and a dependency in the code breaks it. I need to decide whether to fix the "
                + "code so that the dependency goes away, or to allow this dependency as an exception to the rule "
                + "(with a short reason that is stored with the model)."
        );
        // The prompt may be pasted into an AI that can change the code (Claude Code, say), which
        // would take the asked-for change as a task; reading the code makes the verdict better.
        text.AppendLine()
            .AppendLine(
                "This is a review only: do not change, create or commit any files, and do not run commands "
                    + "that do. If you can read the code, use it to check your answer."
            );
        if (Codebase(source) is { } codebase)
            text.AppendLine().AppendLine(codebase);
        AppendRule(text, model, rule);
        AppendAllowed(text, model, rule);
        AppendDependency(text, model, source, target);

        if (sources is { Count: > 0 })
            AppendSource(text, model, sources, "The code of the type that makes the dependency.");

        text.AppendLine().Append(TaskSection);
        return text.ToString();
    }

    // The prompt for an AI that can change the code (Claude Code, say), to remove the dependency
    // as the review suggested: the rule, the dependency, the suggested change with the assumptions
    // to check first, and where to look. It shows no source, since such an AI reads the code itself.
    public static string BuildFix(
        IModel model,
        ArchitectureRule rule,
        RuleViolation violation,
        RuleSuggestion suggestion
    )
    {
        if (Ends(model, violation) is not var (source, target))
            return "";

        var text = new StringBuilder();
        text.AppendLine(
            "Please remove a dependency that breaks an architecture rule in this codebase. Dependinator, a tool "
                + "that maps the structure and dependencies of the code, found it, and a review of it suggested how "
                + "to remove it."
        );
        AppendRule(text, model, rule);
        AppendDependency(text, model, source, target);

        text.AppendLine().AppendLine("## The suggested change").AppendLine();
        text.AppendLine(suggestion.Change);
        if (suggestion.Why != "")
            text.AppendLine().AppendLine($"Why: {suggestion.Why}");
        if (suggestion.Assumptions is not ("" or "None" or "None."))
            text.AppendLine().AppendLine($"Assumptions to check first: {suggestion.Assumptions}");

        var files = GetSourceRequests(model, violation)
            .SelectMany(r => r.Units)
            .Select(u => DisplayPath(model, u.Span.Path))
            .Distinct()
            .ToList();
        if (files.Count > 0)
        {
            text.AppendLine().AppendLine("## Where to look").AppendLine();
            text.AppendLine("The code of the type that makes the dependency:");
            foreach (var file in files)
                text.AppendLine($"- {file}");
        }

        text.AppendLine().Append(FixTaskSection);
        return text.ToString();
    }

    const string FixTaskSection = """
        ## Your task

        1. Check the assumptions above in the code first. If one does not hold, say so, and propose another change before editing.
        2. Make the smallest change that removes the dependency, without changing behavior and without adding another dependency that breaks the rule.
        3. Update the code that refers to what you moved or renamed.
        4. Build, and run the tests.
        5. Summarize what you changed. Dependinator shows whether the rule is kept after its next parse.

        """;

    static readonly string TaskSection = $"""
        ## Your task

        Decide whether this dependency should be fixed (removed from the code) or allowed as an exception to the rule. Base the decision on what the rule protects (layering, say, or keeping a part free of a technology), on what the dependency is used for, and on how hard it would be to remove.

        Answer in this format:

        Verdict: Fix or Allow
        Why: 2–4 sentences.
        Assumptions: what you assumed about code that is not shown here, or "None".
        Reason to store: (only if Allow) at most {MaxReasonLength} characters, specific to this dependency, for a future maintainer; not a general statement about the rule.
        Change: (only if Fix) the smallest concrete change that removes the dependency, for example moving code, depending on an abstraction instead, or passing the data in (describe it; do not make it).

        If the information is not enough to decide, say what you would need to see instead of guessing.

        """;

    static void AppendRule(StringBuilder text, IModel model, ArchitectureRule rule)
    {
        text.AppendLine().AppendLine("## The rule").AppendLine();
        var from = model.Nodes.Values.FirstOrDefault(n => n.Name == rule.FromName);
        var to = model.Nodes.Values.FirstOrDefault(n => n.Name == rule.ToName);
        text.AppendLine(
            $"{Side(from, rule.FromName)} must not use {Side(to, rule.ToName)}: nothing at or inside the first "
                + "may depend on anything at or inside the second."
        );
        var described = new[] { from, to }.OfType<Node>().Where(n => n.Description is not null).ToList();
        if (described.Count == 0)
            return;
        text.AppendLine();
        foreach (var node in described)
            text.AppendLine($"- {node.LongName}{Described(node)}");
    }

    static string Side(Node? node, string name) => node is null ? name : $"{Kind(node.Type)} {node.LongName}";

    // The dependencies the user allowed under the rule before, with why: what is considered fine.
    static void AppendAllowed(StringBuilder text, IModel model, ArchitectureRule rule)
    {
        var allowed = model.AllowedViolations.Where(a => a.IsFor(rule)).ToList();
        if (allowed.Count == 0)
            return;
        text.AppendLine().AppendLine("Dependencies allowed under this rule before, with why:").AppendLine();
        foreach (var a in allowed.Take(MaxAllowedListed))
        {
            var reason = a.Reason == "" ? "no reason given" : $"\"{Clip(a.Reason)}\"";
            text.AppendLine($"- {LongName(model, a.SourceName)} → {LongName(model, a.TargetName)}: {reason}");
        }
        if (allowed.Count > MaxAllowedListed)
            text.AppendLine($"- … and {allowed.Count - MaxAllowedListed} more");
    }

    static string LongName(IModel model, string nodeName) =>
        model.Nodes.Values.FirstOrDefault(n => n.Name == nodeName)?.LongName ?? nodeName;

    static void AppendDependency(StringBuilder text, IModel model, Node source, Node target)
    {
        var links = LinksOf(source, target);
        var count = links.Count;
        text.AppendLine().AppendLine("## The dependency that breaks it").AppendLine();
        text.AppendLine($"{Kind(source.Type)} {source.LongName}{Described(source)}");
        text.AppendLine($"uses {Kind(target.Type)} {target.LongName}{Described(target)}");
        text.AppendLine().AppendLine($"{count} {(count == 1 ? "link" : "links")}:");
        AppendLinks(text, links, model.Root);
    }

    static (Node Source, Node Target)? Ends(IModel model, RuleViolation violation) =>
        model.Nodes.TryGetValue(violation.SourceId, out var source)
        && model.Nodes.TryGetValue(violation.TargetId, out var target)
            ? (source, target)
            : null;

    // The links from the source unit (or its members) to the target unit (or its members).
    static List<Link> LinksOf(Node source, Node target) =>
        [
            .. source
                .DescendantsAndSelfPreOrder()
                .SelectMany(n => n.SourceLinks)
                .Where(l => l.Target == target || l.Target.Ancestors().Contains(target)),
        ];
}

using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Modeling;

// What a re-parse changed, told in a sentence ("2 new nodes, 1 removed, 5 new links"). Node and
// link ids are captured before and after; only differences are reported.
static class ModelChangeSummary
{
    public readonly record struct Snapshot(IReadOnlySet<NodeId> Nodes, IReadOnlySet<LinkId> Links);

    public static Snapshot Capture(IModelMgr modelMgr) =>
        modelMgr.WithModel(m => new Snapshot(m.Nodes.Keys.ToHashSet(), m.Links.Keys.ToHashSet()));

    // Null when nothing changed.
    public static string? Describe(Snapshot before, Snapshot after)
    {
        var addedNodes = after.Nodes.Count(id => !before.Nodes.Contains(id));
        var removedNodes = before.Nodes.Count(id => !after.Nodes.Contains(id));
        var addedLinks = after.Links.Count(id => !before.Links.Contains(id));
        var removedLinks = before.Links.Count(id => !after.Links.Contains(id));
        return Describe(addedNodes, removedNodes, addedLinks, removedLinks);
    }

    public static string? Describe(int addedNodes, int removedNodes, int addedLinks, int removedLinks)
    {
        List<string> parts = [];
        if (addedNodes > 0)
            parts.Add($"{addedNodes} new {Plural("node", addedNodes)}");
        if (removedNodes > 0)
            parts.Add($"{removedNodes} {Plural("node", removedNodes)} removed");
        if (addedLinks > 0)
            parts.Add($"{addedLinks} new {Plural("link", addedLinks)}");
        if (removedLinks > 0)
            parts.Add($"{removedLinks} {Plural("link", removedLinks)} removed");
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    static string Plural(string word, int count) => count == 1 ? word : word + "s";
}

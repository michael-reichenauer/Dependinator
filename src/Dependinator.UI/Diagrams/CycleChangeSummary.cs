using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

// What a re-parse changed about cycles, told in a sentence ("1 cycle to fix is gone, 1 new
// cycle"), so a refactor that broke a cycle, or made one, is noticed without the cycles panel
// open. Open cycles are compared by overlap: one that only grew or shrank is neither new nor
// gone. Accepted and to-fix entries are counted when their cycle went away or a node joined it.
static class CycleChangeSummary
{
    public readonly record struct Snapshot(IReadOnlyList<Cycle> Open, IReadOnlyList<AcceptedCycleReport> Decided);

    public static Snapshot Capture(IModelMgr modelMgr) =>
        modelMgr.WithModel(m =>
        {
            var (open, accepted, toFix, _, _) = CycleService.Compute(m);
            return new Snapshot(open, [.. accepted, .. toFix]);
        });

    // Null when nothing changed.
    public static string? Describe(Snapshot before, Snapshot after)
    {
        var wasOccurring = before.Decided.Where(r => r.StillOccurs).Select(r => r.Accepted).ToHashSet();
        var nowGone = after.Decided.Where(r => r.IsGone && wasOccurring.Contains(r.Accepted)).ToList();
        var changed = after.Decided.Where(r => r.HasChanged && wasOccurring.Contains(r.Accepted)).ToList();

        // A cycle that grew out of an entry is told as that entry changing, not as a new cycle.
        var newCycles = after.Open.Count(c =>
            !before.Open.Any(b => Overlaps(b, c)) && !changed.Any(r => Overlaps(r.Accepted, c))
        );
        var goneCycles = before.Open.Count(b => !after.Open.Any(c => Overlaps(b, c)));
        var toFixGone = nowGone.Count(r => r.Accepted.IsToFix);
        return Describe(newCycles, goneCycles, toFixGone, nowGone.Count - toFixGone, changed.Count);
    }

    public static string? Describe(int newCycles, int goneCycles, int toFixGone, int acceptedGone, int changed)
    {
        List<string> parts = [];
        if (toFixGone > 0)
            parts.Add($"{Cycles(toFixGone)} to fix {IsAre(toFixGone)} gone");
        if (acceptedGone > 0)
            parts.Add($"{acceptedGone} accepted {Plural(acceptedGone)} {IsAre(acceptedGone)} gone");
        if (changed > 0)
            parts.Add($"{changed} accepted or to-fix {Plural(changed)} changed");
        if (goneCycles > 0)
            parts.Add($"{Cycles(goneCycles)} gone");
        if (newCycles > 0)
            parts.Add($"{newCycles} new {Plural(newCycles)}");
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    static bool Overlaps(Cycle a, Cycle b) =>
        a.ContainerNodeName == b.ContainerNodeName && a.Members.Any(m => b.Members.Any(n => n.NodeName == m.NodeName));

    static bool Overlaps(AcceptedCycle entry, Cycle cycle) =>
        entry.ContainerName == cycle.ContainerNodeName
        && cycle.Members.Any(m => entry.MemberNames.Contains(m.NodeName));

    static string Cycles(int count) => $"{count} {Plural(count)}";

    static string Plural(int count) => count == 1 ? "cycle" : "cycles";

    static string IsAre(int count) => count == 1 ? "is" : "are";
}

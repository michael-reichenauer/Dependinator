using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

record NodeSearchResult(NodeId Id, string ShortName, string FullName);

interface INodeSearchService
{
    IReadOnlyList<NodeSearchResult> Search(string query);
}

// Searches all named (non-root) nodes by their short name using a VS Code Ctrl-T style
// fuzzy matcher: typed letters must appear as a subsequence, and uppercase letters favor
// word/camelCase boundaries (e.g. "PZS" jumps to "PanZoomService").
[Scoped]
class NodeSearchService(IModelMgr modelMgr) : INodeSearchService
{
    const int BaseMatch = 10;
    const int PrefixBonus = 20;
    const int BoundaryBonus = 30;
    const int ConsecutiveBonus = 15;
    const int UppercaseBoundaryBonus = 20;
    const int GapPenalty = 2;
    const int MaxGapPenalized = 10;

    public IReadOnlyList<NodeSearchResult> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        query = query.Trim();

        // Snapshot candidate fields under the model lock; score outside the lock.
        var candidates = modelMgr.WithModel(model =>
            model.Nodes.Values.Where(n => !n.IsRoot).Select(n => (n.Id, n.ShortName, n.Name)).ToList()
        );

        var results = new List<(NodeSearchResult Result, int Score)>(candidates.Count);
        foreach (var (id, shortName, name) in candidates)
        {
            var score = FuzzyMatch(query, shortName);
            if (score is null)
                continue;

            results.Add((new NodeSearchResult(id, shortName, name), score.Value));
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Result.ShortName.Length)
            .ThenBy(r => r.Result.ShortName, StringComparer.Ordinal)
            .Select(r => r.Result)
            .ToList();
    }

    // Returns a match score, or null if the query is not a subsequence of the candidate.
    internal static int? FuzzyMatch(string query, string candidate)
    {
        if (query.Length == 0)
            return 0;
        if (candidate.Length == 0)
            return null;

        int score = 0;
        int ci = 0;
        int lastMatch = -2;

        foreach (char qc in query)
        {
            int searchStart = ci;
            int matchIndex = FindNextMatch(candidate, ci, qc);
            if (matchIndex < 0)
                return null; // Not a subsequence match.

            bool isBoundary = IsBoundary(candidate, matchIndex);
            score += BaseMatch;

            if (matchIndex == 0)
                score += PrefixBonus;
            if (isBoundary)
                score += BoundaryBonus;
            if (matchIndex == lastMatch + 1)
                score += ConsecutiveBonus;
            if (char.IsUpper(qc) && isBoundary)
                score += UppercaseBoundaryBonus;

            // Penalize skipped characters so earlier/clustered matches rank higher.
            int gap = matchIndex - searchStart;
            score -= Math.Min(gap, MaxGapPenalized) * GapPenalty;

            lastMatch = matchIndex;
            ci = matchIndex + 1;
        }

        return score;
    }

    // For uppercase query chars, prefer the nearest boundary occurrence (honoring the
    // "uppercase marks a new word" intent); otherwise take the next case-insensitive match.
    static int FindNextMatch(string candidate, int start, char qc)
    {
        if (char.IsUpper(qc))
        {
            for (int i = start; i < candidate.Length; i++)
            {
                if (CharEquals(candidate[i], qc) && IsBoundary(candidate, i))
                    return i;
            }
        }

        for (int i = start; i < candidate.Length; i++)
        {
            if (CharEquals(candidate[i], qc))
                return i;
        }

        return -1;
    }

    static bool CharEquals(char a, char b) => char.ToLowerInvariant(a) == char.ToLowerInvariant(b);

    static bool IsBoundary(string s, int i)
    {
        if (i == 0)
            return true;

        char prev = s[i - 1];
        char cur = s[i];

        if (!char.IsLetterOrDigit(prev))
            return true; // After a separator such as '.', '/', '_', '+', '<'.
        if (char.IsUpper(cur) && !char.IsUpper(prev))
            return true; // camelCase hump.
        if (char.IsDigit(cur) && !char.IsDigit(prev))
            return true; // Letter -> digit boundary.

        return false;
    }
}

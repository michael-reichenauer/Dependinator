using Dependinator.UI.Modeling.Models;

namespace Dependinator.UI.Diagrams;

record NodeSearchResult(NodeId Id, string ShortName, string FullName);

// Searches all named (non-root) nodes by their short name using a VS Code Ctrl-T style
// fuzzy matcher: typed letters must appear as a subsequence, and uppercase letters favor
// word/camelCase boundaries (e.g. "PZS" jumps to "PanZoomService"). When the query looks
// like a qualified path (contains a '.' or '/'), the node's full name is matched too, so
// e.g. "Demo.Core.RootClass" finds the node whose short name is just "RootClass".
interface INodeSearchService
{
    IReadOnlyList<NodeSearchResult> Search(string query);
}

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
    const int MaxResultItems = 100;

    // Separators used in a node's full name (e.g. "Demo.Core.RootClass"); a query containing
    // one is treated as a qualified path and matched against full names too.
    static readonly char[] PathSeparators = ['.', '/'];

    public IReadOnlyList<NodeSearchResult> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        query = query.Trim();

        // Snapshot candidate fields under the model lock; score outside the lock.
        // Pass-through nodes are invisible containers and thus not navigable targets.
        var candidates = modelMgr.WithModel(model =>
            model
                .Nodes.Values.Where(n => !n.IsRoot && !n.IsPassThrough)
                .Select(n => (n.Id, n.ShortName, n.LongName))
                .ToList()
        );

        // For a qualified query like "Demo.Core.RootClass" the short name ("RootClass") is
        // shorter than the query and can never match, so also match the full name in that case.
        bool matchFullName = query.IndexOfAny(PathSeparators) >= 0;

        var results = new List<(NodeSearchResult Result, int Score)>(candidates.Count);
        foreach (var (id, shortName, name) in candidates)
        {
            var score = FuzzyMatch(query, shortName);
            if (score is null && matchFullName)
                score = FuzzyMatch(query, name);
            if (score is null)
                continue;

            results.Add((new NodeSearchResult(id, shortName, name), score.Value));
        }

        var response = results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Result.ShortName.Length)
            .ThenBy(r => r.Result.ShortName, StringComparer.Ordinal)
            .Select(r => r.Result)
            .Take(MaxResultItems)
            .ToList();

        // Log.Info($"Search Query '{query}':\n  {string.Join("\n  ", response.Take(10))}");

        return response;
    }

    // Returns the best match score, or null if the query cannot be matched.
    //
    // Matching rule (camelCase / VS Code Ctrl-T style): every typed character after the
    // first must either continue the current word (be adjacent to the previous match) or
    // start a new word (be at a word boundary). Skipping into the middle of a later word
    // is rejected, so "INavSer" matches "INavigationService" but not the scattered
    // subsequence inside "AddDependinatorServices<TEntryAssemblyMarker>".
    internal static int? FuzzyMatch(string query, string candidate)
    {
        if (query.Length == 0)
            return 0;
        if (candidate.Length < query.Length)
            return null;

        // Cheap reject: anything that is not even a subsequence cannot match.
        if (!IsSubsequence(query, candidate))
            return null;

        // memo[qi, fromIndex] caches the best score for matching query[qi..] starting the
        // search at candidate[fromIndex..]. -1 means "not yet computed".
        var memo = new int?[query.Length + 1, candidate.Length + 1];
        var computed = new bool[query.Length + 1, candidate.Length + 1];

        int? Match(int qi, int fromIndex)
        {
            if (qi == query.Length)
                return 0;
            if (fromIndex >= candidate.Length)
                return null;
            if (computed[qi, fromIndex])
                return memo[qi, fromIndex];

            char qc = query[qi];
            bool isFirst = qi == 0;
            int? best = null;

            for (int k = fromIndex; k < candidate.Length; k++)
            {
                if (!CharEquals(candidate[k], qc))
                    continue;

                bool consecutive = !isFirst && k == fromIndex;
                bool boundary = IsBoundary(candidate, k);

                // After the first char, only continue a word or start a new one.
                if (!isFirst && !consecutive && !boundary)
                    continue;

                int? rest = Match(qi + 1, k + 1);
                if (rest is null)
                    continue;

                int s = ScoreChar(qc, k, fromIndex, consecutive, boundary) + rest.Value;
                if (best is null || s > best)
                    best = s;
            }

            computed[qi, fromIndex] = true;
            memo[qi, fromIndex] = best;
            return best;
        }

        return Match(0, 0);
    }

    static int ScoreChar(char qc, int matchIndex, int fromIndex, bool consecutive, bool boundary)
    {
        int score = BaseMatch;

        if (matchIndex == 0)
            score += PrefixBonus;
        if (boundary)
            score += BoundaryBonus;
        if (consecutive)
            score += ConsecutiveBonus;
        if (char.IsUpper(qc) && boundary)
            score += UppercaseBoundaryBonus;

        // Penalize skipped characters so earlier/tighter matches rank higher.
        int gap = matchIndex - fromIndex;
        score -= Math.Min(gap, MaxGapPenalized) * GapPenalty;

        return score;
    }

    static bool IsSubsequence(string query, string candidate)
    {
        int ci = 0;
        foreach (char qc in query)
        {
            while (ci < candidate.Length && !CharEquals(candidate[ci], qc))
                ci++;
            if (ci >= candidate.Length)
                return false;
            ci++;
        }

        return true;
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

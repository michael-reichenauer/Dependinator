using Dependinator.Core.ModelQueries;

namespace Dependinator.UI.Diagrams.Ai;

// The UI's side of IModelQueryService, the local RPC target the hosts ask (the LSP over the
// tunnel to the VS Code webview, the web host in process). It is one per process, while models
// live per scope (each browser tab of the web host is a scope with its own model), so it answers
// on the scope that opened most recently, until that scope closes.
[Singleton]
sealed class ModelQueryService : IModelQueryService
{
    readonly object syncRoot = new();
    IModelQueries? current;

    public void SetCurrent(IModelQueries queries)
    {
        lock (syncRoot)
            current = queries;
    }

    public void ClearCurrent(IModelQueries queries)
    {
        lock (syncRoot)
        {
            if (current == queries)
                current = null;
        }
    }

    public Task<Result<ModelSummary>> GetModelAsync() => Ask(q => q.GetModel());

    public Task<Result<NodeChildren>> GetChildrenAsync(string nodeName) => Ask(q => q.GetChildren(nodeName));

    public Task<Result<IReadOnlyList<QueryNode>>> SearchNodesAsync(string query) => Ask(q => q.SearchNodes(query));

    public Task<Result<NodeDependencies>> GetDependenciesAsync(string nodeName, bool isIncoming) =>
        Ask(q => q.GetDependencies(nodeName, isIncoming));

    public Task<Result<PathsResult>> FindPathsAsync(string fromName, string toName) =>
        Ask(q => q.FindPaths(fromName, toName));

    public Task<Result<IReadOnlyList<CycleResult>>> ListCyclesAsync() => Ask(q => q.ListCycles());

    public Task<Result<IReadOnlyList<RuleResult>>> ListRulesAsync() => Ask(q => q.ListRules());

    public Task<Result<RuleResult>> CheckRuleAsync(string fromName, string toName) =>
        Ask(q => q.CheckRule(fromName, toName));

    Task<Result<T>> Ask<T>(Func<IModelQueries, Result<T>> question)
        where T : notnull
    {
        IModelQueries? queries;
        lock (syncRoot)
            queries = current;
        if (queries is null)
            return Task.FromResult<Result<T>>(
                new Error("No Dependinator diagram is open. Open the diagram and try again.")
            );
        try
        {
            return Task.FromResult(question(queries));
        }
        catch (Exception e)
        {
            // A question must not take down the RPC connection the diagram also runs on.
            return Task.FromResult<Result<T>>(new Error(e));
        }
    }
}

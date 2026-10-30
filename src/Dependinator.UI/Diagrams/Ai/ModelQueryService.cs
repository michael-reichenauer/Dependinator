using Dependinator.Core.ModelQueries;

namespace Dependinator.UI.Diagrams.Ai;

// The UI's side of IModelQueryService, the local RPC target the hosts ask (the LSP over the
// tunnel to the VS Code webview, the web host in process). It is one per process, while models
// live per scope (each browser tab of the web host is a scope with its own model), so it answers
// on the scope that opened most recently, and when that one closes, on the one before.
[Singleton]
sealed class ModelQueryService : ModelQueryAnswers, IModelQueryService
{
    readonly object syncRoot = new();
    readonly List<IModelQueries> scopes = [];

    public void Add(IModelQueries queries)
    {
        lock (syncRoot)
        {
            scopes.Remove(queries);
            scopes.Add(queries);
        }
    }

    public void Remove(IModelQueries queries)
    {
        lock (syncRoot)
            scopes.Remove(queries);
    }

    protected override IModelQueries? Scope()
    {
        lock (syncRoot)
            return scopes.LastOrDefault();
    }
}

// The questions on one scope's queries (ScopeModelQueries: the e2e test mode's stand-in agent asks
// its own scope, which a parallel test's newer scope must not take over).
sealed class ScopeModelQueries(IModelQueries queries) : ModelQueryAnswers
{
    protected override IModelQueries? Scope() => queries;
}

// IModelQueryService's questions, each told to the scope first, in words ("Searched for …"), for a
// panel that shows an agent's progress. Only ModelQueryService declares the interface: the RPC
// setup serves every class that does, and ScopeModelQueries is no service.
abstract class ModelQueryAnswers
{
    protected abstract IModelQueries? Scope();

    public Task<Result<ModelSummary>> GetModelAsync() => Ask("Looked at the model", q => q.GetModel());

    public Task<Result<NodeChildren>> GetChildrenAsync(string nodeName) =>
        Ask(
            nodeName == "" ? "Listed the top level" : $"Listed what is in {Short(nodeName)}",
            q => q.GetChildren(nodeName)
        );

    public Task<Result<IReadOnlyList<QueryNode>>> SearchNodesAsync(string query) =>
        Ask($"Searched for \"{query}\"", q => q.SearchNodes(query));

    public Task<Result<NodeDependencies>> GetDependenciesAsync(string nodeName, bool isIncoming) =>
        Ask(
            isIncoming ? $"Looked at what uses {Short(nodeName)}" : $"Looked at what {Short(nodeName)} uses",
            q => q.GetDependencies(nodeName, isIncoming)
        );

    public Task<Result<PathsResult>> FindPathsAsync(string fromName, string toName) =>
        Ask($"Looked for paths from {Short(fromName)} to {Short(toName)}", q => q.FindPaths(fromName, toName));

    public Task<Result<CycleList>> ListCyclesAsync() => Ask("Listed the cycles", q => q.ListCycles());

    public Task<Result<IReadOnlyList<RuleResult>>> ListRulesAsync() => Ask("Listed the rules", q => q.ListRules());

    public Task<Result<RuleResult>> CheckRuleAsync(string fromName, string toName) =>
        Ask($"Checked the rule {Short(fromName)} must not use {Short(toName)}", q => q.CheckRule(fromName, toName));

    public Task<Result<NodeSource>> ReadSourceAsync(string nodeName) =>
        AskAsync($"Read the code of {Short(nodeName)}", q => q.ReadSourceAsync(nodeName));

    // A node name as a progress line shows it: its last part ("AppBar", not its full name).
    static string Short(string name)
    {
        var withoutParameters = name.Split('(')[0];
        var cut = withoutParameters.LastIndexOf('.');
        return cut >= 0 && cut < withoutParameters.Length - 1 ? name[(cut + 1)..] : name;
    }

    Task<Result<T>> Ask<T>(string what, Func<IModelQueries, Result<T>> question)
        where T : notnull => AskAsync(what, q => Task.FromResult(question(q)));

    async Task<Result<T>> AskAsync<T>(string what, Func<IModelQueries, Task<Result<T>>> question)
        where T : notnull
    {
        if (Scope() is not { } queries)
            return new Error("No Dependinator diagram is open. Open the diagram and try again.");
        try
        {
            queries.ReportAsked(what);
            return await question(queries);
        }
        catch (Exception e)
        {
            // A question must not take down the RPC connection the diagram also runs on.
            return new Error(e);
        }
    }
}

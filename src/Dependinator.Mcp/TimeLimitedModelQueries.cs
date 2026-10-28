using Dependinator.Core.ModelQueries;

namespace Dependinator.Mcp;

// Puts a time limit on each question to the UI. In VS Code the questions go to the webview, and
// while it is closed nobody answers: the RPC call would wait forever, and the agent with it. A
// lost connection becomes an error the agent can act on, too. isConnected, when given, checks
// quickly that the other side is there at all, so the agent need not wait out the whole limit.
sealed class TimeLimitedModelQueries(IModelQueryService inner, TimeSpan timeLimit, Func<Task<bool>>? isConnected = null)
    : IModelQueryService
{
    public const string NoAnswerMessage =
        "The Dependinator diagram did not answer. Open it in VS Code (command \"Dependinator: Open\") and try again.";

    public Task<Result<ModelSummary>> GetModelAsync() => Ask(inner.GetModelAsync);

    public Task<Result<NodeChildren>> GetChildrenAsync(string nodeName) => Ask(() => inner.GetChildrenAsync(nodeName));

    public Task<Result<IReadOnlyList<QueryNode>>> SearchNodesAsync(string query) =>
        Ask(() => inner.SearchNodesAsync(query));

    public Task<Result<NodeDependencies>> GetDependenciesAsync(string nodeName, bool isIncoming) =>
        Ask(() => inner.GetDependenciesAsync(nodeName, isIncoming));

    public Task<Result<PathsResult>> FindPathsAsync(string fromName, string toName) =>
        Ask(() => inner.FindPathsAsync(fromName, toName));

    public Task<Result<CycleList>> ListCyclesAsync() => Ask(inner.ListCyclesAsync);

    public Task<Result<IReadOnlyList<RuleResult>>> ListRulesAsync() => Ask(inner.ListRulesAsync);

    public Task<Result<RuleResult>> CheckRuleAsync(string fromName, string toName) =>
        Ask(() => inner.CheckRuleAsync(fromName, toName));

    async Task<Result<T>> Ask<T>(Func<Task<Result<T>>> question)
        where T : notnull
    {
        using var cts = new CancellationTokenSource(timeLimit);
        try
        {
            if (isConnected is not null && !await isConnected().WithCancellation(cts.Token))
                return new Error(NoAnswerMessage);
            return await question().WithCancellation(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return new Error(NoAnswerMessage);
        }
        catch (Exception e)
        {
            // The diagram is there but the call failed: the cause helps more than "open it".
            Log.Warn($"Model query failed: {e}");
            var cause = e.Message.Split('\n', 2)[0].Trim();
            return new Error($"The Dependinator diagram could not answer: {cause}", e);
        }
    }
}

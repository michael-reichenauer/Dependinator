using Dependinator.Core.Rpc;

namespace Dependinator.Core.ModelQueries;

// Questions an AI agent asks about the model open in the diagram: its structure, who depends on
// whom, the paths between two parts, the cycles, and the architecture rules, including what a
// rule that is not there yet would break. Dependinator's MCP server offers each method as a tool,
// and in-app AI features can call the same methods. The model, its rules and the cycle decisions
// live in the UI (the webview in VS Code), so the UI implements this and the hosts ask it over
// the RPC tunnel; every method returns an error when no diagram with a model is open. (In the UI
// this interface resolves to the RPC proxy, so in-app features use the UI's IModelQueries.)
//
// Nodes are named by QueryNode.Name, the key every method takes. A method also accepts a node's
// Title, or the Alias the user gave it, when no other node has the same one. Answers are kept small for a model's context: lists
// are capped, and the totals say how much was left out.
[Rpc]
interface IModelQueryService
{
    // The model's file, its size, and its top-level nodes.
    Task<Result<ModelSummary>> GetModelAsync();

    // The node and its direct children; the model's top level for an empty name.
    Task<Result<NodeChildren>> GetChildrenAsync(string nodeName);

    // Nodes whose name matches the query, best first (fuzzy, as the diagram's search box).
    Task<Result<IReadOnlyList<QueryNode>>> SearchNodesAsync(string query);

    // What the node uses (or, incoming, what uses it), grouped at the level where the two sides
    // meet: a namespace's dependency on a type in another project is listed under that project,
    // one on a sibling namespace under the sibling. Each group gives a few type pairs as examples.
    Task<Result<NodeDependencies>> GetDependenciesAsync(string nodeName, bool isIncoming);

    // The shortest chains of type hops by which "from" depends on "to" (the first few, by name).
    Task<Result<PathsResult>> FindPathsAsync(string fromName, string toName);

    // Circular dependencies: the open ones, and those the user accepted or marked to fix.
    Task<Result<CycleList>> ListCyclesAsync();

    // The model's architecture rules with their violations and allowed dependencies.
    Task<Result<IReadOnlyList<RuleResult>>> ListRulesAsync();

    // Evaluates "from must not use to" without adding it: the violations it would report today.
    Task<Result<RuleResult>> CheckRuleAsync(string fromName, string toName);
}

// A node as the answers show it: Name is the key to pass back, Title the readable name, Type the
// node's kind (Assembly, Namespace, ClassType, Resource, ...). Alias is the name the user gave it,
// which the diagram shows instead. Description is shortened.
record QueryNode(
    string Name,
    string Title,
    string Type,
    int ChildCount,
    string? Description = null,
    string? Alias = null
);

record ModelSummary(
    string Path,
    int NodeCount,
    int LinkCount,
    int RuleCount,
    int OpenCycleCount,
    IReadOnlyList<QueryNode> TopLevel,
    int TopLevelCount
);

record NodeChildren(QueryNode Node, IReadOnlyList<QueryNode> Children, int ChildCount);

// Two units (types, or other nodes that hold members) and the number of links between them.
record UnitDependency(string Source, string Target, int LinkCount);

record DependencyGroup(QueryNode Node, int LinkCount, IReadOnlyList<UnitDependency> Examples);

record NodeDependencies(QueryNode Node, bool IsIncoming, IReadOnlyList<DependencyGroup> Groups, int GroupCount);

// Status is Found, NotFound or Nested (one contains the other: "depends on" has no meaning).
// Each path lists the node names from a unit inside "from" to a unit inside "to".
record PathsResult(string Status, IReadOnlyList<IReadOnlyList<string>> Paths);

// Status is Open, Accepted (intended, Reason says why) or ToFix (Reason is the planned change).
// A decision is Outgrown when nodes joined its cycle (the larger cycle is listed as Open), and is
// kept with StillOccurs false when its cycle no longer occurs. Members may be capped: MemberCount
// says how many there are.
record CycleResult(
    string Container,
    IReadOnlyList<string> Members,
    int MemberCount,
    string Status,
    string? Reason,
    bool StillOccurs
);

// Open cycles first, then those to fix, then the accepted ones; Count is how many there are in all.
record CycleList(IReadOnlyList<CycleResult> Cycles, int Count);

record AllowedDependency(string Source, string Target, string Reason, int LinkCount);

// IsRule says whether the model has the rule already; IsResolved whether both nodes exist.
// ViolationCount and LinkCount cover every violation, Violations only the largest ones;
// AllowedCount counts every allowed dependency, Allowed only the first ones.
record RuleResult(
    string From,
    string To,
    bool IsRule,
    bool IsResolved,
    int ViolationCount,
    int LinkCount,
    IReadOnlyList<UnitDependency> Violations,
    IReadOnlyList<AllowedDependency> Allowed,
    int AllowedCount
);

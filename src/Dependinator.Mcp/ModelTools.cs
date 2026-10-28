using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dependinator.Core.ModelQueries;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Dependinator.Mcp;

// The MCP tools: each one asks IModelQueryService (the open diagram, over the RPC tunnel in VS
// Code) and answers with compact JSON. A failed question becomes an error result with its message,
// which the agent reads and can act on (search for a name, open the diagram). The descriptions
// are what the agent sees: they say when to use a tool and how names work, not how it is built.
[McpServerToolType]
sealed class ModelTools(IModelQueryService queries)
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [McpServerTool(Name = "get_model", ReadOnly = true, Idempotent = true)]
    [Description(
        "Summarizes the model open in Dependinator: its file, node, link, rule and open cycle counts, and its "
            + "top-level nodes (projects, or Bicep files). Start here."
    )]
    public async Task<CallToolResult> GetModel() => Answer(await queries.GetModelAsync());

    [McpServerTool(Name = "list_children", ReadOnly = true, Idempotent = true)]
    [Description(
        "Lists a node's direct children (a project's namespaces, a namespace's types, a type's members), "
            + "or the top level when nodeName is empty."
    )]
    public async Task<CallToolResult> ListChildren(
        [Description("The node's name, as in another answer's \"name\"; empty for the top level.")] string nodeName = ""
    ) => Answer(await queries.GetChildrenAsync(nodeName));

    [McpServerTool(Name = "search_nodes", ReadOnly = true, Idempotent = true)]
    [Description(
        "Finds nodes by name, best match first (fuzzy, like the diagram's search: \"PZS\" finds PanZoomService; "
            + "a dotted query also matches full names). Use it to get the name other tools take."
    )]
    public async Task<CallToolResult> SearchNodes([Description("Part of a node's name.")] string query) =>
        Answer(await queries.SearchNodesAsync(query));

    [McpServerTool(Name = "get_dependencies", ReadOnly = true, Idempotent = true)]
    [Description(
        "Lists what a node uses (or with incoming, what uses it), grouped at the level where the two sides "
            + "meet: a dependency on a type in another project is listed under that project, one on a sibling "
            + "namespace under the sibling. Each group has a link count and a few type pairs as examples. "
            + "Members count for their type."
    )]
    public async Task<CallToolResult> GetDependencies(
        [Description("The node's name.")] string nodeName,
        [Description("True for what uses the node instead of what it uses.")] bool incoming = false
    ) => Answer(await queries.GetDependenciesAsync(nodeName, incoming));

    [McpServerTool(Name = "find_paths", ReadOnly = true, Idempotent = true)]
    [Description(
        "Explains why one node depends on another: the shortest chains of type-to-type dependencies from "
            + "inside \"from\" to inside \"to\" (status Found, NotFound, or Nested when one contains the other)."
    )]
    public async Task<CallToolResult> FindPaths(
        [Description("The depending node's name.")] string from,
        [Description("The name of the node it may depend on.")] string to
    ) => Answer(await queries.FindPathsAsync(from, to));

    [McpServerTool(Name = "list_cycles", ReadOnly = true, Idempotent = true)]
    [Description(
        "Lists circular dependencies between sibling nodes (projects, namespaces, types): the open ones, and "
            + "those the user marked to fix (reason = the planned change) or accepted as intended (reason = why)."
    )]
    public async Task<CallToolResult> ListCycles() => Answer(await queries.ListCyclesAsync());

    [McpServerTool(Name = "list_rules", ReadOnly = true, Idempotent = true)]
    [Description(
        "Lists the model's architecture rules (\"from must not use to\") with their current violations per type "
            + "pair, and the dependencies the user allowed with a reason."
    )]
    public async Task<CallToolResult> ListRules() => Answer(await queries.ListRulesAsync());

    [McpServerTool(Name = "check_rule", ReadOnly = true, Idempotent = true)]
    [Description(
        "Evaluates the rule \"from must not use to\" without adding it: the violations it would report today. "
            + "Use it to test a rule before suggesting it, or to check that a change keeps a layering."
    )]
    public async Task<CallToolResult> CheckRule(
        [Description("The name of the node that must not depend on the other.")] string from,
        [Description("The name of the node it must not use.")] string to
    ) => Answer(await queries.CheckRuleAsync(from, to));

    // An error is a result, not an exception, so an expected answer such as "no diagram is open"
    // is not logged as a failure.
    static CallToolResult Answer<T>(Result<T> result)
        where T : notnull
    {
        var isError = result.Value is not T;
        var text = result.Value is T value ? JsonSerializer.Serialize(value, JsonOptions) : result.Error.Message;
        return new CallToolResult { Content = [new TextContentBlock { Text = text }], IsError = isError };
    }
}

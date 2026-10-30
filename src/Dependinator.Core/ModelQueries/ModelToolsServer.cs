namespace Dependinator.Core.ModelQueries;

// Where this process serves the model tools (Dependinator.Mcp), so a Claude Code run that the app
// starts itself can be given them (--mcp-config): the dev host's /mcp, or the LSP's own server
// with its token. Hosts that serve no tools leave it unset.
interface IModelToolsServer
{
    ModelToolsAddress? Address { get; }

    void SetAddress(ModelToolsAddress address);
}

// The MCP endpoint's URL, and the bearer token it requires, if any.
record ModelToolsAddress(string Url, string? Token);

[Singleton]
class ModelToolsServer : IModelToolsServer
{
    public ModelToolsAddress? Address { get; private set; }

    public void SetAddress(ModelToolsAddress address) => Address = address;
}

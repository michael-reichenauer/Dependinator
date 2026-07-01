using Dependinator.UI.Shared;

// Blazor WebAssembly host: the browser-hosted entry point and Web UI (also embedded in the VS Code
// extension). Provides browser-specific host services, such as the (no-op) file system, since local
// file access is not available in the browser.
namespace Dependinator.Wasm;

class BrowserHostFileSystem : IHostFileSystem
{
    public bool Exists(string path) => false;

    public Stream OpenRead(string path) =>
        throw new NotSupportedException("Local file access is not available in the browser host.");
}

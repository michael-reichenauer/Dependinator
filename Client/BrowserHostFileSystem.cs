using Dependinator.Shared;

namespace Dependinator.Client;

class BrowserHostFileSystem : IHostFileSystem
{
    public bool Exists(string path) => false;

    public Stream OpenRead(string path) =>
        throw new NotSupportedException("Local file access is not available in the browser host.");
}

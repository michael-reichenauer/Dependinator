using Dependinator.Shared;

namespace Dependinator.Web;

class LocalHostFileSystem : IHostFileSystem
{
    public bool Exists(string path) => File.Exists(path);

    public Stream OpenRead(string path) => File.OpenRead(path);
}

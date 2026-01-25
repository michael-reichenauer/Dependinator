using ICSharpCode.Decompiler.TypeSystem;

namespace Dependinator.Shared;

interface IHostFileSystem
{
    bool Exists(string path);
    Stream OpenRead(string path);
}

[Singleton]
class NoHostFileSystem : IHostFileSystem
{
    public bool Exists(string path) => false;

    public Stream OpenRead(string path) =>
        throw new NotSupportedException("Local file access is not available in this host.");
}

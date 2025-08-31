using System.Reflection;

namespace Dependinator.Utils;

public interface IEmbeddedResources
{
    Stream OpenResource(string name);
    Stream? TryOpenResource(string name);
}

internal class EmbeddedResources<T> : IEmbeddedResources
{
    static readonly Assembly assembly = typeof(T).Assembly;

    public Stream OpenResource(string name)
    {
        var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            var all = string.Join("\n  - ", assembly.GetManifestResourceNames());
            throw new InvalidOperationException($"Resource '{name}' not found.\nAvailable:\n  - {all}");
        }
        return stream;
    }

    public Stream? TryOpenResource(string name) => assembly.GetManifestResourceStream(name);
}

using System.Reflection;

namespace DependinatorCore.Utils;

// public interface IEmbeddedResources
// {
//     Stream OpenResourceStream(string name);
//     Stream? TryOpenResource(string name);
// }

// public class EmbeddedResources<T> : IEmbeddedResources
// {
//     static readonly Assembly assembly = typeof(T).Assembly;

//     public Stream OpenResourceStream(string name)
//     {
//         Log.Info("Opening", name);
//         var stream = assembly.GetManifestResourceStream(name);
//         if (stream is null)
//         {
//             var all = string.Join("\n  - ", assembly.GetManifestResourceNames());
//             throw new InvalidOperationException($"Resource '{name}' not found.\nAvailable:\n  - {all}");
//         }
//         return stream;
//     }

//     public Stream? TryOpenResource(string name) => assembly.GetManifestResourceStream(name);
// }

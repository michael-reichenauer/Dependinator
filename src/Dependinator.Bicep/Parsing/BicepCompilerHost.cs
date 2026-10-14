using Bicep.Core;
using Bicep.Core.Semantics;
using Bicep.IO.Abstraction;

namespace Dependinator.Bicep.Parsing;

// Owns the one Bicep compiler of the process. BicepCompiler.Create() builds the compiler's own
// service container (file system, configuration, resource type providers, module registries), kept
// private here rather than merged into Dependinator's container. The first compilation also loads
// the Azure resource type index, which takes a few seconds, so the compiler is created lazily and
// reused by every parse; each compilation re-reads the source files from disk.
[Singleton]
class BicepCompilerHost
{
    readonly Lazy<BicepCompiler> compiler = new(
        () => BicepCompiler.Create(),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    // Compiles the entry file and the local modules it references. Registry (br:/ts:) modules are
    // not restored, since that needs network access and credentials; they stay unresolved and the
    // model shows them as leaf module nodes.
    public Compilation Compile(string entryPath) =>
        compiler.Value.CreateCompilationWithoutRestore(IOUri.FromFilePath(Path.GetFullPath(entryPath)));
}

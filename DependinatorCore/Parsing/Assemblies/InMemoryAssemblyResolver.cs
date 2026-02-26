using ICSharpCode.Decompiler.Metadata;

namespace Dependinator.Core.Parsing.Assemblies;

sealed class InMemoryAssemblyResolver : ICSharpCode.Decompiler.Metadata.IAssemblyResolver
{
    readonly Dictionary<string, MetadataFile> assemblies = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryAssemblyResolver(params MetadataFile[] modules)
    {
        foreach (var module in modules)
            Register(module);
    }

    public void Register(MetadataFile module)
    {
        var metadata = module.Metadata;
        if (!metadata.IsAssembly)
            return;

        var def = metadata.GetAssemblyDefinition();
        var name = metadata.GetString(def.Name);
        assemblies[name] = module;
    }

    public MetadataFile? Resolve(IAssemblyReference reference)
    {
        if (assemblies.TryGetValue(reference.Name, out var file))
            return file;

        return null; // let the decompiler fall back to unresolved metadata
    }

    public MetadataFile? ResolveModule(MetadataFile mainModule, string moduleName) => null;

    public Task<MetadataFile?> ResolveAsync(IAssemblyReference reference) => Task.FromResult(Resolve(reference));

    public Task<MetadataFile?> ResolveModuleAsync(MetadataFile mainModule, string moduleName) =>
        Task.FromResult<MetadataFile?>(null);
}

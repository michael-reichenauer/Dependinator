using Mono.Cecil;

namespace Dependinator.Model.Parsers.Assemblies;

internal class ParsingAssemblyResolver : DefaultAssemblyResolver
{
    public override AssemblyDefinition Resolve(AssemblyNameReference name)
    {
        try
        {
            return base.Resolve(name);
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to resolve {name}, {e.Message}");
        }

        return null!;
    }


    public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
    {
        try
        {
            return base.Resolve(name, parameters);
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to resolve {name}, {e.Message}");
        }

        return null!;
    }
}


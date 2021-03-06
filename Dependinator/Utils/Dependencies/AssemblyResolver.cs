using System;
using System.IO;
using System.Reflection;
using Dependinator.Utils.OsSystem;


namespace Dependinator.Utils.Dependencies
{
    internal class AssemblyResolver
    {
        private static string assembliesPath;


        public static void Activate(Assembly assembly)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
            string location = Path.GetDirectoryName(assembly.Location);
            assembliesPath = Path.Combine(location, assembly.GetFileVersion());
        }


        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            string resolveName = null;
            try
            {
                resolveName = args.Name.Split(',')[0];

                string path = Path.Combine(assembliesPath, $"{resolveName}.dll");
                if (!File.Exists(path))
                {
                    if (!resolveName.EndsWith(".resources"))
                    {
                        Log.Warn($"Failed to resolve assembly {resolveName} from {assembliesPath}");
                    }

                    return null;
                }

                byte[] buffer = File.ReadAllBytes(path);
                return Assembly.Load(buffer);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to load {resolveName} from {assembliesPath}, {e}");
                throw;
            }
        }
    }
}

using System;
using System.IO;
using System.Reflection;


namespace Dependinator.Utils
{
	internal class AssemblyResolver
	{
		public static void Activate()
		{
			AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
		}

	
		private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
		{
			try
			{
				Assembly executingAssembly = Assembly.GetExecutingAssembly();
				string name = executingAssembly.FullName.Split(',')[0];
				string resolveName = args.Name.Split(',')[0];
				string resourceName = $"{name}.Dependencies.{resolveName}.dll";

				// Load the requested assembly from the resources
				using (Stream stream = executingAssembly.GetManifestResourceStream(resourceName))
				{
					if (stream == null)
					{
						if (!resolveName.EndsWith(".resources"))
						{
							Log.Warn($"Failed to resolve assembly {resolveName}");
						}

						return null;
					}

					byte[] buffer = new byte[stream.Length];
					stream.Read(buffer, 0, buffer.Length);
					// Log.Debug($"Resolved {resolveName}");
					return Assembly.Load(buffer);
				}
			}
			catch (Exception e)
			{
				Log.Error($"Failed to load, {e}");
				throw;
			}
		}
	}
}
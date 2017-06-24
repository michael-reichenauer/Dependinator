using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Dependiator.Utils;


namespace Dependiator.Modeling.Analyzing.Private
{
	internal static class Assemblies
	{
		public static void RegisterReferencedAssembliesHandler()
		{
			AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += ResolveReferencedAssemblies;
		}


		public static void UnregisterReferencedAssembliesHandler()
		{
			AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= ResolveReferencedAssemblies;
		}


		public static Assembly LoadAssembly(string path)
		{
			Log.Debug($"Try load {path}");
			return Assembly.ReflectionOnlyLoadFrom(path);
		}


		public static string GetErrorMessage(string path, ReflectionTypeLoadException e)
		{
			var missingAssemblies = e.LoaderExceptions
				.Select(l => l.Message)
				.Distinct()
				.Select(ToAssemblyName)
				.ToList();

			int maxCount = 10;
			int count = missingAssemblies.Count;
			string names = string.Join("\n   ", missingAssemblies.Take(maxCount));
			if (count > maxCount)
			{
				names += "\n   ...";
			}

			string message =
				$"Failed to load '{path}'\n" +
				$"Could not locate {count} referenced assemblies:\n" +
				$"   {names}";
			return message;
		}


		public static string ToAssemblyName(string errorMessage)
		{
			int index = errorMessage.IndexOf('\'');
			int index2 = errorMessage.IndexOf(',', index + 1);

			string name = errorMessage.Substring(index + 1, (index2 - index - 1));
			return name;
		}


		private static Assembly ResolveReferencedAssemblies(object sender, ResolveEventArgs args)
		{
			AssemblyName assemblyName = new AssemblyName(args.Name);

			if (assemblyName.Name == "Dependiator.resources")
			{
				return null;
			}

			Assembly assembly;

			string path = assemblyName.Name + ".dll";

			if (File.Exists(path) && TryGetAssemblyByFile(path, out assembly))
			{
				// Log.Debug($"Resolve assembly by file {assemblyName + ".dll"}");
				return assembly;
			}

			if (TryGetAssemblyByName(assemblyName, out assembly))
			{
				// Log.Debug($"Resolve assembly by name {args.Name}");
				return assembly;
			}

			if (TryLoadFromResources(args, out assembly))
			{
				Log.Warn($"Resolve assembly from resources {args.Name}");
				return assembly;
			}

			Log.Error($"Failed to resolve assembly {args.Name}");

			return null;
		}


		private static bool TryGetAssemblyByName(AssemblyName assemblyName, out Assembly assembly)
		{
			try
			{
				Log.Debug($"Try load {assemblyName}");
				assembly = Assembly.ReflectionOnlyLoad(assemblyName.FullName);
				return true;
			}
			catch (Exception)
			{
				assembly = null;
				return false;
			}
		}


		private static bool TryGetAssemblyByFile(string path, out Assembly assembly)
		{
			try
			{
				Log.Debug($"Try load {path}");
				assembly = Assembly.ReflectionOnlyLoadFrom(path);
				return true;
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to load {path}, {e.GetType()}, {e.Message}");
				assembly = null;
				return false;
			}
		}


		private static bool TryLoadFromResources(ResolveEventArgs args, out Assembly assembly)
		{
			Assembly executingAssembly = Assembly.GetExecutingAssembly();

			if (args.RequestingAssembly.FullName != executingAssembly.FullName)
			{
				// Requesting assembly is not Dependiator, no need to check resources
				assembly = null;
				return false;
			}

			string name = executingAssembly.FullName.Split(',')[0];
			string resolveName = args.Name.Split(',')[0];
			string resourceName = $"{name}.Dependencies.{resolveName}.dll";

			// Try load the requested assembly from the resources
			using (Stream stream = executingAssembly.GetManifestResourceStream(resourceName))
			{
				if (stream == null)
				{
					// Assembly not embedded in the resources
					assembly = null;
					return false;
				}

				// Load assembly from resources
				byte[] buffer = new byte[stream.Length];
				stream.Read(buffer, 0, buffer.Length);

				assembly = Assembly.ReflectionOnlyLoad(buffer);
				return true;
			}
		}
	}
}
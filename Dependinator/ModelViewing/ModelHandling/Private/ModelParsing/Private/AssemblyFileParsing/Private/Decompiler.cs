using System;
using System.Collections.Concurrent;
using Dependinator.Utils;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Mono.Cecil;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelParsing.Private.AssemblyFileParsing.Private
{
	public class Decompiler
	{
		private readonly ConcurrentDictionary<ModuleDefinition, CSharpDecompiler> decompilers =
			new ConcurrentDictionary<ModuleDefinition, CSharpDecompiler>();


		public Lazy<string> LazyDecompile(TypeDefinition type)
		{
			return new Lazy<string>(() => GetDecompiledText(type));
		}

		public Lazy<string> LazyDecompile(IMemberDefinition member)
		{
			return new Lazy<string>(() => GetDecompiledText(member));
		}


		public Lazy<string> LazyDecompile(string name)
		{
			return new Lazy<string>(() => GetDecompiledText(name));
		}



		private string GetDecompiledText(string name)
		{
			try
			{
				//CSharpDecompiler decompiler = GetDecompiler(type.Module);

				//string text = decompiler.DecompileTypesAsString(new[] { type }).Replace("\t", "  ");

				//return text;
				return null;
			}
			catch (Exception e)
			{
				Log.Error($"Failed to decompile {name}, {e.Message}");
				return $"Error: Failed to decompile {name},\n{e.Message}";
			}
		}


		private string GetDecompiledText(TypeDefinition type)
		{
			try
			{
				CSharpDecompiler decompiler = GetDecompiler(type.Module);

				string text = decompiler.DecompileTypesAsString(new[] { type }).Replace("\t", "  ");

				return text;
			}
			catch (Exception e)
			{
				Log.Error($"Failed to decompile {type}, {e.Message}");
				return $"Error: Failed to decompile {type},\n{e.Message}";
			}
		}


		private string GetDecompiledText(IMemberDefinition member)
		{
			try
			{
				CSharpDecompiler decompiler = GetDecompiler(member.DeclaringType.Module);

				string text = decompiler.DecompileAsString(member).Replace("\t", "  ");

				return text;
			}
			catch (Exception e)
			{
				Log.Error($"Failed to decompile {member}, {e.Message}");
				return $"Error: Failed to decompile {member},\n{e.Message}";
			}
		}


		private CSharpDecompiler GetDecompiler(ModuleDefinition module) =>
			decompilers.GetOrAdd(
				module,
				key => new CSharpDecompiler(key, new DecompilerSettings(LanguageVersion.Latest)));
	}
}
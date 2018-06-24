using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.DataHandling.Private.Parsing.Private.AssemblyParsing.Private;
using Dependinator.Utils;
using Dependinator.Utils.ErrorHandling;
using Mono.Cecil;


namespace Dependinator.ModelViewing.DataHandling.Private.Parsing.Private.AssemblyParsing
{
	internal class AssemblyParser : IDisposable
	{
		private readonly string assemblyPath;
		private readonly Decompiler decompiler = new Decompiler();
		private readonly AssemblyModuleParser assemblyModuleParser;
		private readonly TypeParser typeParser;
		private readonly MemberParser memberParser;
		private readonly ParsingAssemblyResolver resolver = new ParsingAssemblyResolver();
		private readonly Lazy<AssemblyDefinition> assembly;

		private List<TypeInfo> typeInfos = new List<TypeInfo>();


		public AssemblyParser(
			string assemblyPath,
			string assemblyRootGroup,
			DataItemsCallback itemsCallback)
		{
			this.assemblyPath = assemblyPath;

			XmlDocParser xmlDockParser = new XmlDocParser(assemblyPath);
			LinkHandler linkHandler = new LinkHandler(itemsCallback);

			assemblyModuleParser = new AssemblyModuleParser(assemblyRootGroup, linkHandler, itemsCallback);
			typeParser = new TypeParser(linkHandler, xmlDockParser, itemsCallback);
			memberParser = new MemberParser(linkHandler, xmlDockParser, itemsCallback);

			assembly = new Lazy<AssemblyDefinition>(GetAssembly);
		}


		public string ModuleName => Name.GetModuleName(assembly.Value);


		public void ParseModule()
		{
			assemblyModuleParser.AddModule(assembly.Value);
		}


		public void ParseModuleReferences()
		{
			if (assembly.Value == null)
			{
				return;
			}

			assemblyModuleParser.AddModuleReferences();
		}


		public void ParseTypes()
		{
			if (assembly.Value == null)
			{
				return;
			}

			IEnumerable<TypeDefinition> assemblyTypes = GetAssemblyTypes();

			// Add assembly type nodes (including inner type types)
			typeInfos = assemblyTypes.SelectMany(t => typeParser.AddType(assembly.Value, t)).ToList();
		}


		public void ParseTypeMembers()
		{
			typeParser.AddTypesLinks(typeInfos);
			memberParser.AddTypesMembers(typeInfos);
		}


		public void Dispose()
		{
			assembly.Value?.Dispose();
		}


		public R<string> GetCodeAsync(NodeName nodeName) => 
			decompiler.GetCodeAsync(assembly.Value.MainModule, nodeName);


		private AssemblyDefinition GetAssembly()
		{
			try
			{
				ReaderParameters parameters = new ReaderParameters
				{
					AssemblyResolver = resolver,
				};

				return AssemblyDefinition.ReadAssembly(assemblyPath, parameters);
			}
			catch (Exception e)
			{
				Log.Exception(e, $"Failed to load '{assemblyPath}'");
			}

			return null;
		}


		private IEnumerable<TypeDefinition> GetAssemblyTypes() =>
			assembly.Value.MainModule.Types
			.Where(type =>
				!Name.IsCompilerGenerated(type.Name) &&
				!Name.IsCompilerGenerated(type.DeclaringType?.Name));
	}
}
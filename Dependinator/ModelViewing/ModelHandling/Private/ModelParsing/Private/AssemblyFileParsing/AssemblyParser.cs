using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.ModelViewing.ModelHandling.Private.ModelParsing.Private.AssemblyFileParsing.Private;
using Dependinator.Utils;
using Mono.Cecil;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelParsing.Private.AssemblyFileParsing
{
	internal class AssemblyParser : IDisposable
	{
		private readonly string assemblyPath;
		private List<TypeInfo> typeInfos = new List<TypeInfo>();
		private readonly Decompiler decompiler = new Decompiler();

		private readonly AssemblyModuleParser assemblyModuleParser;
		private readonly TypeParser typeParser;
		private readonly MemberParser memberParser;
		private AssemblyDefinition assembly;
		private ParsingAssemblyResolver resolver;


		public AssemblyParser(
			string assemblyPath,
			string assemblyRootGroup,
			ModelItemsCallback itemsCallback)
		{
			this.assemblyPath = assemblyPath;

			XmlDocParser xmlDockParser = new XmlDocParser(assemblyPath);
			LinkHandler linkHandler = new LinkHandler(itemsCallback);

			assemblyModuleParser = new AssemblyModuleParser(assemblyRootGroup, linkHandler, itemsCallback);
			typeParser = new TypeParser(linkHandler, xmlDockParser, decompiler, assemblyPath, itemsCallback);
			memberParser = new MemberParser(linkHandler, xmlDockParser, decompiler, assemblyPath, itemsCallback );
		}


		public void ParseModule()
		{
			try
			{
				if (!File.Exists(assemblyPath))
				{
					Log.Warn($"File '{assemblyPath}' does not exists");
					return;
				}

				resolver = new ParsingAssemblyResolver();
				ReaderParameters parameters = new ReaderParameters
				{
					AssemblyResolver = resolver,
				};

				assembly = AssemblyDefinition.ReadAssembly(assemblyPath, parameters);

				assemblyModuleParser.AddModule(assembly);
			}
			catch (Exception e)
			{
				Log.Exception(e, $"Failed to load '{assemblyPath}'");
			}
		}


		public void ParseModuleReferences()
		{
			if (assembly == null)
			{
				return;
			}

			assemblyModuleParser.AddModuleReferences();
		}


		public void ParseTypes()
		{
			if (assembly == null)
			{
				return;
			}

			IEnumerable<TypeDefinition> assemblyTypes = GetAssemblyTypes();

			// Add assembly type nodes (including inner type types)
			typeInfos = assemblyTypes.SelectMany(typeParser.AddType).ToList();
		}


		public void ParseTypeMembers()
		{
			typeParser.AddTypesLinks(typeInfos);
			memberParser.AddTypesMembers(typeInfos);
		}


		public void Dispose()
		{
			assembly?.Dispose();
		}


		private IEnumerable<TypeDefinition> GetAssemblyTypes() => 
			assembly.MainModule.Types
			.Where(type =>
				!Name.IsCompilerGenerated(type.Name) &&
				!Name.IsCompilerGenerated(type.DeclaringType?.Name));
	}
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dependinator.ModelHandling.Core;
using Dependinator.ModelHandling.Private.ModelParsing.Private.AssemblyFileParsing.Private;
using Dependinator.Utils;
using Mono.Cecil;


namespace Dependinator.ModelHandling.Private.ModelParsing.Private.AssemblyFileParsing
{
	internal class AssemblyParser
	{
		private readonly string assemblyPath;
		private List<TypeInfo> typeInfos = new List<TypeInfo>();

		private readonly ModuleParser moduleParser;
		private readonly TypeParser typeParser;
		private readonly MemberParser memberParser;


		public AssemblyParser(
			string assemblyPath,
			string assemblyRootGroup,
			ModelItemsCallback itemsCallback)
		{
			this.assemblyPath = assemblyPath;

			XmlDocParser xmlDockParser = new XmlDocParser(assemblyPath);
			LinkHandler linkHandler = new LinkHandler(itemsCallback);

			moduleParser = new ModuleParser(assemblyRootGroup, linkHandler, itemsCallback);
			typeParser = new TypeParser(linkHandler, xmlDockParser, itemsCallback);
			memberParser = new MemberParser(linkHandler, xmlDockParser, itemsCallback);
		}


		public void ParseTypes()
		{
			try
			{
				if (!File.Exists(assemblyPath))
				{
					Log.Warn($"File '{assemblyPath}' does not exists");
					return;
				}

				AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

				moduleParser.AddModule(assembly);

				ParseTypes(assembly);
			}
			catch (Exception e)
			{
				Log.Exception(e, $"Failed to load '{assemblyPath}'");
			}
		}



		public void ParseTypeMembers()
		{
			typeParser.AddTypesLinks(typeInfos);
			memberParser.AddTypesMembers(typeInfos);
		}



		private void ParseTypes(AssemblyDefinition assembly)
		{
			IEnumerable<TypeDefinition> assemblyTypes = assembly.MainModule.Types
				.Where(type =>
					!Name.IsCompilerGenerated(type.Name) &&
					!Name.IsCompilerGenerated(type.DeclaringType?.Name));

			// Add assembly type nodes (including inner type types)
			typeInfos = assemblyTypes.SelectMany(typeParser.AddTypes).ToList();
		}
	}
}
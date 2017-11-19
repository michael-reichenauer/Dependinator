using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dependinator.ModelHandling.Core;
using Dependinator.Utils;
using Mono.Cecil;


namespace Dependinator.ModelHandling.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class AssemblyParser
	{
		private readonly string assemblyPath;
		private List<TypeInfo> typeInfos = new List<TypeInfo>();
		
		private readonly LinkHandler linkHandler;
		private readonly ModuleParser moduleParser;
		private readonly TypeParser typeParser;
		private readonly MemberParser memberParser;


		public AssemblyParser(
			string assemblyPath,
			string assemblyRootGroup,
			ModelItemsCallback itemsCallback)
		{
			this.assemblyPath = assemblyPath;

			Sender sender = new Sender(itemsCallback);

			linkHandler = new LinkHandler(sender);
			moduleParser = new ModuleParser(assemblyRootGroup, linkHandler, sender);
			typeParser = new TypeParser(linkHandler, sender);
			memberParser = new MemberParser(linkHandler, sender);
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

				AddAssemblyTypes(assembly);
			}
			catch (Exception e)
			{
				Log.Exception(e, $"Failed to load '{assemblyPath}'");
			}
		}


		public void ParseAssemblyModuleReferences()
		{
			moduleParser.AddModuleLinks();
		}


		public void ParseTypeMembers()
		{
			Timing t = new Timing();

			typeParser.AddTypesLinks(typeInfos);
			memberParser.AddTypesMembers(typeInfos);
			

			//t.Log($"Added {sender.NodesCount} nodes in {assembly.Name.Name}");
		}

		
		public void ParseLinks()
		{
			Timing t = new Timing();

			linkHandler.SendAllLinks();

			//t.Log($"Added {sender.LinkCount} links in {assembly.Name.Name}");
		}


		private void AddAssemblyTypes(AssemblyDefinition assembly)
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
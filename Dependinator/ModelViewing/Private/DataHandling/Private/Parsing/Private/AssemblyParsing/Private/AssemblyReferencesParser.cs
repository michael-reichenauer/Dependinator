using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Mono.Cecil;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.AssemblyParsing.Private
{
	internal class AssemblyReferencesParser
	{
		private readonly LinkHandler linkHandler;
		private readonly DataItemsCallback itemsCallback;

		public AssemblyReferencesParser(LinkHandler linkHandler, DataItemsCallback itemsCallback)
		{

			this.linkHandler = linkHandler;
			this.itemsCallback = itemsCallback;
		}


		public void AddReferences(
			AssemblyDefinition assembly,
			IReadOnlyList<string> internalModules)
		{
			DataNodeName sourceAssemblyName = (DataNodeName)Name.GetModuleName(assembly);

			var externalReferences = GetExternalAssemblyReferences(assembly, internalModules);

			if (externalReferences.Any())
			{
				DataNodeName referencesRootName = SendReferencesRootNode();

				foreach (AssemblyNameReference reference in externalReferences)
				{
					DataNodeName referenceName = (DataNodeName)Name.GetModuleName(reference);
					DataNodeName parent = GetReferenceParent(referencesRootName, referenceName);

					DataNode referenceNode = new DataNode(referenceName, parent, NodeType.Assembly);

					itemsCallback(referenceNode);

					linkHandler.AddLink(sourceAssemblyName, (string)referenceName, NodeType.Assembly);
				}
			}
		}


		private DataNodeName SendReferencesRootNode()
		{
			DataNodeName referencesRootName = (DataNodeName)"$References";
			DataNode referencesRootNode = new DataNode(
				referencesRootName, DataNodeName.None, NodeType.Group)
			{ Description = "External references" };

			itemsCallback(referencesRootNode);
			return referencesRootName;
		}


		private DataNodeName GetReferenceParent(DataNodeName parent, DataNodeName referenceName)
		{
			string[] parts = ((string)referenceName).Split("*".ToCharArray());

			for (int i = 0; i < parts.Length - 1; i++)
			{
				string name = string.Join(".", parts.Take(i + 1));

				DataNodeName groupName = (DataNodeName)$"{(string)parent}.{name}";
				DataNode groupNode = new DataNode(groupName, parent, NodeType.Group);

				itemsCallback(groupNode);
				parent = groupName;
			}

			return parent;
		}


		private static IReadOnlyList<AssemblyNameReference> GetExternalAssemblyReferences(
			AssemblyDefinition assembly,
			IReadOnlyList<string> internalModules)
		{
			return assembly.MainModule.AssemblyReferences
				.Where(reference => !IgnoredTypes.IsSystemIgnoredModuleName(reference.Name))
				.Where(reference => !internalModules.Contains(Name.GetModuleName(reference)))
				.ToList();
		}
	}
}

using System;
using System.Linq;
using System.Reflection;
using Dependinator.ModelViewing.ModelHandling.Core;
using Mono.Cecil;
using Mono.Collections.Generic;


namespace Dependinator.ModelViewing.ModelDataHandling.Private.Parsing.Private.AssemblyParsing.Private
{
	internal class AssemblyModuleParser
	{

		private readonly string rootGroup;
		private readonly LinkHandler linkHandler;
		private readonly DataItemsCallback itemsCallback;
		private AssemblyDefinition assembly;

		public AssemblyModuleParser(
			string rootGroup,
			LinkHandler linkHandler,
			DataItemsCallback itemsCallback)
		{
			this.rootGroup = rootGroup;
			this.linkHandler = linkHandler;
			this.itemsCallback = itemsCallback;
		}


		public void AddModule(AssemblyDefinition assemblyDefinition)
		{
			assembly = assemblyDefinition;

			string parent = rootGroup;
			string moduleName = Name.GetAssemblyName(assembly);

			int index = moduleName.IndexOfTxt("*");
			if (index > 0)
			{
				string groupName = moduleName.Substring(1, index - 1);
				parent = parent == null ? groupName : $"{parent}.{groupName}";
			}

			parent = parent != null ? $"${parent?.Replace(".", ".$")}" : null;

			string description = GetDescription();
			NodeName nodeName = NodeName.From(moduleName);
			NodeId nodeId = new NodeId(nodeName);
			DataNode moduleNode = new DataNode(nodeId, nodeName, parent, NodeType.NameSpace, description, null);
			itemsCallback(moduleNode);
		}


		private string GetDescription()
		{
			Collection<CustomAttribute> attributes = assembly.CustomAttributes;

			CustomAttribute descriptionAttribute = attributes.FirstOrDefault(attribute =>
				attribute.AttributeType.FullName == typeof(AssemblyDescriptionAttribute).FullName);

			CustomAttributeArgument? argument = descriptionAttribute?.ConstructorArguments
				.FirstOrDefault();

			string assemblyDescription = argument?.Value as string;

			string description = $"Assembly: {assemblyDescription}";
	
			return description;
		}


		public void AddModuleReferences()
		{
			string moduleName = Name.GetAssemblyName(assembly);
			NodeName moduleNodeName = NodeName.From(moduleName);
			NodeId moduleId = new NodeId(moduleNodeName);

			var references = assembly.MainModule.AssemblyReferences.
				Where(reference => !IgnoredTypes.IsSystemIgnoredModuleName(reference.Name));

			if (references.Any())
			{
				string description = "External references";
				NodeName nodeName = NodeName.From("$References");
				NodeId nodeId = new NodeId(nodeName);
				DataNode referencesNode = new DataNode(nodeId, nodeName, null, NodeType.NameSpace, description, null);
				itemsCallback(referencesNode);
			}

			foreach (AssemblyNameReference reference in references)
			{
				string parent = "References";
				string referenceName = Name.GetModuleName(reference);

				int index = referenceName.IndexOfTxt("*");
				if (index > 0)
				{
					parent = $"References.{referenceName.Substring(0,index)}";
				}

				parent = $"${parent?.Replace(".", ".$")}";

				string description = "Assembly";
				NodeName referenceNodeName = NodeName.From(referenceName);
				NodeId referenceId = new NodeId(referenceNodeName);
				DataNode referenceNode = new DataNode(referenceId, referenceNodeName, parent, NodeType.NameSpace, description, null);
				itemsCallback(referenceNode);

				linkHandler.AddLink(moduleId, referenceName, NodeType.NameSpace);
			}
		}
	}
}
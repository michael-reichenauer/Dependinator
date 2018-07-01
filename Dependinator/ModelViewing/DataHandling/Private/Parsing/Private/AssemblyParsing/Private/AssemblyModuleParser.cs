using System;
using System.Linq;
using System.Reflection;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.Nodes;
using Mono.Cecil;
using Mono.Collections.Generic;


namespace Dependinator.ModelViewing.DataHandling.Private.Parsing.Private.AssemblyParsing.Private
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
			string moduleName = Name.GetModuleName(assembly);

			int index = moduleName.IndexOfTxt("*");
			if (index > 0)
			{
				string groupName = moduleName.Substring(1, index - 1);
				parent = parent == null ? groupName : $"{parent}.{groupName}";
			}

			parent = parent != null ? $"${parent?.Replace(".", ".$")}" : null;

			string description = GetDescription();
			DataNodeName nodeName = new DataNodeName(moduleName);
			DataNode moduleNode = new DataNode(
				nodeName,
				parent != null ? new DataNodeName(parent) : null,
				DataNodeType.NameSpace,
				false)
			{ Description = description };
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
			string moduleName = Name.GetModuleName(assembly);
			DataNodeName moduleNodeName = new DataNodeName(moduleName);


			var references = assembly.MainModule.AssemblyReferences.
				Where(reference => !IgnoredTypes.IsSystemIgnoredModuleName(reference.Name));

			if (references.Any())
			{
				string description = "External references";
				DataNodeName nodeName = new DataNodeName("$References");
				DataNode referencesNode = new DataNode(nodeName, null, DataNodeType.NameSpace, false)
					{Description = description};
				itemsCallback(referencesNode);
			}

			foreach (AssemblyNameReference reference in references)
			{
				string parent = "References";
				string referenceName = Name.GetModuleName(reference);

				int index = referenceName.IndexOfTxt("*");
				if (index > 0)
				{
					parent = $"References.{referenceName.Substring(0, index)}";
				}

				parent = $"${parent?.Replace(".", ".$")}";

				string description = "Assembly";
				DataNodeName referenceNodeName = new DataNodeName(referenceName);

				DataNode referenceNode = new DataNode(
					referenceNodeName, 
					parent != null ? new DataNodeName(parent) : null, 
					DataNodeType.NameSpace, false)
					{ Description =  description};
				itemsCallback(referenceNode);

				linkHandler.AddLink(moduleNodeName, referenceName, DataNodeType.NameSpace);
			}
		}
	}
}
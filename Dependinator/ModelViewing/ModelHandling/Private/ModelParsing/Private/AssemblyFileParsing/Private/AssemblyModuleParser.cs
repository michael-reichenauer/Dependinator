using System;
using System.Linq;
using System.Reflection;
using Dependinator.ModelViewing.ModelHandling.Core;
using Mono.Cecil;
using Mono.Collections.Generic;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelParsing.Private.AssemblyFileParsing.Private
{
	internal class AssemblyModuleParser
	{
		private readonly string rootGroup;
		private readonly LinkHandler linkHandler;
		private readonly ModelItemsCallback itemsCallback;
		private AssemblyDefinition assembly;

		public AssemblyModuleParser(
			string rootGroup,
			LinkHandler linkHandler,
			ModelItemsCallback itemsCallback)
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
			ModelNode moduleNode = new ModelNode(moduleName, parent, NodeType.NameSpace, description, null);
			itemsCallback(moduleNode);
		}


		private string GetDescription()
		{
			Collection<CustomAttribute> attributes = assembly.CustomAttributes;

			CustomAttribute descriptionAttribute = attributes.FirstOrDefault(attribute =>
				attribute.AttributeType.FullName == typeof(AssemblyDescriptionAttribute).FullName);

			CustomAttributeArgument? argument = descriptionAttribute?.ConstructorArguments
				.FirstOrDefault();

			string description = argument?.Value as string;
			return description;
		}


		public void AddModuleReferences()
		{
			string moduleName = Name.GetAssemblyName(assembly);

			var references = assembly.MainModule.AssemblyReferences.
				Where(reference => !IgnoredTypes.IsSystemIgnoredModuleName(reference.Name));

			foreach (AssemblyNameReference reference in references)
			{
				string parent = "References";
				string referenceName = Name.GetModuleName(reference);

				int index = referenceName.IndexOfTxt("*");
				if (index > 0)
				{
					parent = $"References.{referenceName.Substring(1, index - 1)}";
				}

				parent = $"${parent?.Replace(".", ".$")}";

				ModelNode referenceNode = new ModelNode(referenceName, parent, NodeType.NameSpace, null, null);
				itemsCallback(referenceNode);

				linkHandler.AddLink(
					new ModelLink(moduleName, referenceName, NodeType.NameSpace));
			}
		}
	}
}
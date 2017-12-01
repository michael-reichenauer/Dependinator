using System;
using System.Linq;
using Dependinator.ModelHandling.Core;
using Dependinator.ModelHandling.ModelPersistence;
using Dependinator.ModelHandling.ModelPersistence.Private.Serializing;
using Mono.Cecil;


namespace Dependinator.ModelHandling.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class ModuleParser
	{
		private readonly string rootGroup;
		private readonly LinkHandler linkHandler;
		private readonly Sender sender;
		private AssemblyDefinition assembly;

		public ModuleParser(
			string rootGroup,
			LinkHandler linkHandler,
			Sender sender)
		{
			this.rootGroup = rootGroup;
			this.linkHandler = linkHandler;
			this.sender = sender;
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

			ModelNode moduleNode = new ModelNode(moduleName, parent, NodeType.NameSpace, null);
			sender.SendNode(moduleNode);
		}


		public void AddModuleLinks()
		{
			if (assembly == null)
			{
				return;
			}

			string moduleName = Name.GetAssemblyName(assembly);

			var references = assembly.MainModule.AssemblyReferences.
				Where(reference => !IgnoredTypes.IsSystemIgnoredModuleName(reference.Name));

			foreach (AssemblyNameReference reference in references)
			{
				string parent = "References";
				string referenceName = Name.GetModuleName(reference.Name);

				int index = referenceName.IndexOfTxt("*");
				if (index > 0)
				{
					parent = $"References.{referenceName.Substring(1, index - 1)}";
				}

				parent =  $"${parent?.Replace(".", ".$")}";

				ModelNode referenceNode = new ModelNode(referenceName, parent, NodeType.NameSpace, null);
				sender.SendNode(referenceNode);

				linkHandler.AddLinkToReference(
					new Reference(moduleName, referenceName, NodeType.NameSpace));
			}
		}
	}
}
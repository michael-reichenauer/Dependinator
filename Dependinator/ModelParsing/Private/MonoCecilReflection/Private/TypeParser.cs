using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Private;
using Dependinator.Utils;
using Mono.Cecil;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class TypeParser
	{
		private readonly Sender sender;
		private readonly LinkHandler linkHandler;


		public TypeParser(LinkHandler linkHandler, Sender sender)
		{
			this.sender = sender;
			this.linkHandler = linkHandler;
		}


		public IEnumerable<TypeInfo> AddTypes(TypeDefinition type)
		{
			string name = Name.GetTypeFullName(type);
			bool isPrivate = type.Attributes.HasFlag(TypeAttributes.NestedPrivate);
			string parent = isPrivate ? $"{NodeName.From(name).ParentName.FullName}.$Private" : null;

			ModelNode typeNode = sender.SendNode(name, parent, JsonTypes.NodeType.Type);

			yield return new TypeInfo(type, typeNode);

			// Iterate all nested types as well
			foreach (var nestedType in type.NestedTypes
				.Where(member => !Name.IsCompilerGenerated(member.Name)))
			{
				foreach (var types in AddTypes(nestedType))
				{
					yield return types;
				}
			}
		}


		public void AddTypesLinks(IEnumerable<TypeInfo> typeInfos)
		{
			typeInfos.ForEach(AddLinksToBaseTypes);
		}


		private void AddLinksToBaseTypes(TypeInfo typeInfo)
		{
			TypeDefinition type = typeInfo.Type;
			ModelNode sourceNode = typeInfo.Node;

			try
			{
				TypeReference baseType = type.BaseType;
				if (baseType != null && baseType.FullName != "System.Object")
				{
					linkHandler.AddLinkToType(sourceNode, baseType);
				}

				type.Interfaces
					.ForEach(interfaceType => linkHandler.AddLinkToType(sourceNode, interfaceType));
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add base type for {type} in {sourceNode.Name}, {e}");
			}
		}
	}
}
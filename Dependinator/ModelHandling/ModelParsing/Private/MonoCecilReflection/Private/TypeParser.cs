using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Common;
using Dependinator.ModelHandling.Core;
using Dependinator.ModelHandling.ModelPersistence;
using Dependinator.ModelHandling.ModelPersistence.Private.Serializing;
using Dependinator.Utils;
using Mono.Cecil;


namespace Dependinator.ModelHandling.ModelParsing.Private.MonoCecilReflection.Private
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
			bool isCompilerGenerated = Name.IsCompilerGenerated(type.Name);
			bool isAsyncStateType = false;
			ModelNode typeNode = null;

			if (isCompilerGenerated)
			{
				// Check if the type is a async state machine type
				isAsyncStateType = type.Interfaces.Any(it => it.Name == "IAsyncStateMachine");

				if (!isAsyncStateType)
				{
					// Some internal compiler generated type, which is ignored for now
					yield break;
				}
			}
			else
			{
				string name = Name.GetTypeFullName(type);
				bool isPrivate = type.Attributes.HasFlag(TypeAttributes.NestedPrivate);
				string parent = isPrivate ? $"{NodeName.From(name).ParentName.FullName}.$Private" : null;

				typeNode = new ModelNode(name, parent, NodeType.Type);
				sender.SendNode(typeNode);
			}

			yield return new TypeInfo(type, typeNode, isAsyncStateType);

			// Iterate all nested types as well
			foreach (var nestedType in type.NestedTypes)
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
			if (typeInfo.IsAsyncStateType)
			{
				// Internal async/await helper type,
				return;
			}

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
using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Common;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;
using Mono.Cecil;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelParsing.Private.AssemblyFileParsing.Private
{
	internal class TypeParser
	{
		private readonly LinkHandler linkHandler;
		private readonly XmlDocParser xmlDockParser;
		private readonly ModelItemsCallback itemsCallback;


		public TypeParser(
			LinkHandler linkHandler, 
			XmlDocParser xmlDockParser, 
			ModelItemsCallback itemsCallback)
		{
			this.linkHandler = linkHandler;
			this.xmlDockParser = xmlDockParser;
			this.itemsCallback = itemsCallback;
		}


		public IEnumerable<TypeInfo> AddType(TypeDefinition type)
		{
			bool isCompilerGenerated = Name.IsCompilerGenerated(type.Name);
			bool isAsyncStateType = false;
			ModelNode typeNode = null;

			if (isCompilerGenerated)
			{
				// Check if the type is a async state machine type
				isAsyncStateType = type.Interfaces.Any(it => it.InterfaceType.Name == "IAsyncStateMachine");

				// AsyncStateTypes are only partially included. The state types are not included as nodes,
				// but are parsed to extract internal types and references. 
				if (!isAsyncStateType)
				{
					// Some other internal compiler generated type, which is ignored for now
					// Log.Warn($"Exclude compiler type {type.Name}");
					yield break;
				}
			}
			else
			{
				if (string.IsNullOrEmpty(type.Namespace))
				{

				}

				string name = Name.GetTypeFullName(type);
				bool isPrivate = type.Attributes.HasFlag(TypeAttributes.NestedPrivate);
				string parent = isPrivate ? $"{NodeName.From(name).ParentName.FullName}.$Private" : null;
				string description = xmlDockParser.GetDescription(name);

				if (type.Name == "NamespaceDoc")
				{
					if (!string.IsNullOrEmpty(description))
					{
						name = Name.GetTypeNamespaceFullName(type);
						typeNode = new ModelNode(name, parent, NodeType.NameSpace, description);
						itemsCallback(typeNode);
					}

					yield break;
				}


				typeNode = new ModelNode(name, parent, NodeType.Type, description);
				itemsCallback(typeNode);
			}

			yield return new TypeInfo(type, typeNode, isAsyncStateType);

			// Iterate all nested types as well
			foreach (var nestedType in type.NestedTypes)
			{
				// Adding a type could result in multiple types
				foreach (var types in AddType(nestedType))
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
					.ForEach(interfaceType => linkHandler.AddLinkToType(sourceNode, interfaceType.InterfaceType));
			}
			catch (Exception e)
			{
				Log.Warn($"Failed to add base type for {type} in {sourceNode.Name}, {e}");
			}
		}
	}
}
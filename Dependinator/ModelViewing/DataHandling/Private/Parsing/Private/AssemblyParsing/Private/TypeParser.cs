using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;
using Mono.Cecil;


namespace Dependinator.ModelViewing.DataHandling.Private.Parsing.Private.AssemblyParsing.Private
{
	internal class TypeParser
	{
		private readonly LinkHandler linkHandler;
		private readonly XmlDocParser xmlDockParser;
		private readonly DataItemsCallback itemsCallback;


		public TypeParser(
			LinkHandler linkHandler,
			XmlDocParser xmlDockParser,
			DataItemsCallback itemsCallback)
		{
			this.linkHandler = linkHandler;
			this.xmlDockParser = xmlDockParser;

			this.itemsCallback = itemsCallback;
		}


		public IEnumerable<TypeInfo> AddType(AssemblyDefinition assembly, TypeDefinition type)
		{
			bool isCompilerGenerated = Name.IsCompilerGenerated(type.Name);
			bool isAsyncStateType = false;
			DataNode typeNode = null;

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
				string name = Name.GetTypeFullName(type);
				bool isPrivate = type.Attributes.HasFlag(TypeAttributes.NestedPrivate);
				DataNodeName parent = isPrivate 
					? new DataNodeName($"{NodeName.From(name).ParentName.FullName}.$private")  
					: null;
				string description = xmlDockParser.GetDescription(name);

				if (IsNameSpaceDocType(type, description))
				{
					// Type was a namespace doc type, extract it and move to next type
					yield break;
				}

				DataNodeName nodeName = new DataNodeName(name);
				typeNode = new DataNode(nodeName, parent, DataNodeType.Type, false)
				{ Description = description };
				itemsCallback(typeNode);
			}

			yield return new TypeInfo(type, typeNode, isAsyncStateType);

			// Iterate all nested types as well
			foreach (var nestedType in type.NestedTypes)
			{
				// Adding a type could result in multiple types
				foreach (var types in AddType(assembly, nestedType))
				{
					yield return types;
				}
			}
		}



		private bool IsNameSpaceDocType(TypeDefinition type, string description)
		{
			if (type.Name.IsSameIgnoreCase("NamespaceDoc"))
			{
				if (!string.IsNullOrEmpty(description))
				{
					string name = Name.GetTypeNamespaceFullName(type);
					DataNodeName nodeName = new DataNodeName(name);
					DataNode node = new DataNode(nodeName, null, DataNodeType.NameSpace, false)
					{ Description = description };
					itemsCallback(node);
				}

				return true;
			}

			return false;
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
			DataNode sourceNode = typeInfo.Node;

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
				Log.Exception(e, $"Failed to add base type for {type} in {sourceNode.Name}");
			}
		}
	}
}
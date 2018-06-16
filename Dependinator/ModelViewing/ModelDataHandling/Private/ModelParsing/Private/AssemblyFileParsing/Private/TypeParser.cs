using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;
using Mono.Cecil;


namespace Dependinator.ModelViewing.ModelDataHandling.Private.ModelParsing.Private.AssemblyFileParsing.Private
{
	internal class TypeParser
	{
		private readonly LinkHandler linkHandler;
		private readonly XmlDocParser xmlDockParser;
		private readonly Decompiler decompiler;
		private readonly string assemblyPath;
		private readonly DataItemsCallback itemsCallback;

	

		public TypeParser(
			LinkHandler linkHandler, 
			XmlDocParser xmlDockParser,
			Decompiler decompiler,
			string assemblyPath,
			DataItemsCallback itemsCallback)
		{
			this.linkHandler = linkHandler;
			this.xmlDockParser = xmlDockParser;
			this.decompiler = decompiler;
			this.assemblyPath = assemblyPath;
			this.itemsCallback = itemsCallback;
		}


		public IEnumerable<TypeInfo> AddType(TypeDefinition type)
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
				if (string.IsNullOrEmpty(type.Namespace))
				{

				}

				string name = Name.GetTypeFullName(type);
				bool isPrivate = type.Attributes.HasFlag(TypeAttributes.NestedPrivate);
				string parent = isPrivate ? $"{NodeName.From(name).ParentName.FullName}.$private" : null;
				string description = xmlDockParser.GetDescription(name);
				
				if (IsNameSpaceDocType(type, description))
				{
					// Type was a namespace doc type, extract it and move to next type
					yield break;
				}

				Lazy<string> codeText = decompiler.LazyDecompile(type, assemblyPath);
				NodeName nodeName = NodeName.From(name);
				NodeId nodeId = new NodeId(nodeName);
				typeNode = new DataNode(nodeId, nodeName, parent, NodeType.Type, description, codeText);
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



		private bool IsNameSpaceDocType(TypeDefinition type, string description)
		{
			if (type.Name.IsSameIgnoreCase("NamespaceDoc"))
			{
				if (!string.IsNullOrEmpty(description))
				{
					string name = Name.GetTypeNamespaceFullName(type);
					NodeName nodeName = NodeName.From(name);
					NodeId nodeId = new NodeId(nodeName);
					DataNode node = new DataNode(nodeId, nodeName, null, NodeType.NameSpace, description, null);
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
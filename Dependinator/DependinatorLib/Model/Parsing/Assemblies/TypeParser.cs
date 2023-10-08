using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Utils;
using Mono.Cecil;
using Dependinator.Model.Parsing;

namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Assemblies.Private
{
    internal class TypeParser
    {
        private readonly LinkHandler linkHandler;
        private readonly XmlDocParser xmlDockParser;
        private readonly Action<NodeData> nodeCallback;


        public TypeParser(
            LinkHandler linkHandler,
            XmlDocParser xmlDockParser,
            Action<NodeData> nodeCallback)
        {
            this.linkHandler = linkHandler;
            this.xmlDockParser = xmlDockParser;
            this.nodeCallback = nodeCallback;
        }


        public IEnumerable<TypeData> AddType(AssemblyDefinition assembly, TypeDefinition type)
        {
            bool isCompilerGenerated = Name.IsCompilerGenerated(type.Name);
            bool isAsyncStateType = false;
            NodeData typeNode = null;

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
                string parent = isPrivate
                    ? $"{NodeName.From(name).ParentName.FullName}.$private" : null;
                string description = xmlDockParser.GetDescription(name);

                if (IsNameSpaceDocType(type, description))
                {
                    // Type was a namespace doc type, extract it and move to next type
                    yield break;
                }

                typeNode = new NodeData(name, parent, NodeData.TypeType, description);
                nodeCallback(typeNode);
            }

            yield return new TypeData(type, typeNode, isAsyncStateType);

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
            if (type.Name.IsSameIc("NamespaceDoc"))
            {
                if (!string.IsNullOrEmpty(description))
                {
                    string name = Name.GetTypeNamespaceFullName(type);
                    NodeData node = new NodeData(name, null, NodeData.NameSpaceType, description);
                    nodeCallback(node);
                }

                return true;
            }

            return false;
        }


        public void AddTypesLinks(IEnumerable<TypeData> typeInfos)
        {
            typeInfos.ForEach(AddLinksToBaseTypes);
        }


        private void AddLinksToBaseTypes(TypeData typeData)
        {
            if (typeData.IsAsyncStateType)
            {
                // Internal async/await helper type,
                return;
            }

            TypeDefinition type = typeData.Type;
            NodeData sourceNode = typeData.Node;

            try
            {
                TypeReference baseType = type.BaseType;
                if (baseType != null && baseType.FullName != "System.Object")
                {
                    linkHandler.AddLinkToType(sourceNode.Name, baseType);
                }

                type.Interfaces
                    .ForEach(interfaceType => linkHandler.AddLinkToType(sourceNode.Name, interfaceType.InterfaceType));
            }
            catch (Exception e)
            {
                Log.Exception(e, $"Failed to add base type for {type} in {sourceNode.Name}");
            }
        }
    }
}

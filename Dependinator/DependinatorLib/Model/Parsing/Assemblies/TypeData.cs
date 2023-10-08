﻿using Mono.Cecil;
using Dependinator.Model.Parsing;


namespace Dependinator.Model.Parsers.Assemblies;

internal class TypeData
{
    public TypeData(TypeDefinition type, NodeData node, bool isAsyncStateType)
    {
        Type = type;
        Node = node;
        IsAsyncStateType = isAsyncStateType;
    }


    public TypeDefinition Type { get; }
    public NodeData Node { get; }
    public bool IsAsyncStateType { get; }
}


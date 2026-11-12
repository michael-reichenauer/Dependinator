// The model items the parser prints, in the JSON shape of Dependinator.Core's Parsing.Item
// (src/Dependinator.Core/Parsing/Types.cs): property names and enum values as declared there.

export type NodeType =
    | "Solution"
    | "Namespace"
    | "Type"
    | "ClassType"
    | "InterfaceType"
    | "EnumType"
    | "FieldMember"
    | "ConstructorMember"
    | "PropertyMember"
    | "MethodMember"
    | "VariableMember"
    | "SourceFile"
    | "FunctionMember"
    | "Folder";

export interface FileSpan {
    Path: string;
    StartLine: number; // 0-based, like the other parsers
    EndLine: number;
}

export interface NodeProperties {
    Type: NodeType;
    Description?: string;
    IsPrivate?: boolean;
    Signature?: string;
    FileSpan?: FileSpan;
}

export interface LinkProperties {
    TargetType?: NodeType;
    IsInheritance?: boolean;
}

export interface Item {
    Node?: { Name: string; Properties: NodeProperties };
    Link?: { Source: string; Target: string; Properties: LinkProperties };
    Problem?: { Message: string; ErrorCount: number; NodeName?: string };
}

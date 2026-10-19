namespace Dependinator.Core.Parsing;

static class NodeTypeExtensions
{
    extension(NodeType nodeType)
    {
        public bool IsMember =>
            nodeType
                is NodeType.FieldMember
                    or NodeType.ConstructorMember
                    or NodeType.EventMember
                    or NodeType.MethodMember
                    or NodeType.PropertyMember
                    or NodeType.ParameterMember
                    or NodeType.VariableMember
                    or NodeType.OutputMember
                    or NodeType.ResourceMember;

        public bool IsType =>
            nodeType
                is NodeType.Type
                    or NodeType.ClassType
                    or NodeType.InterfaceType
                    or NodeType.EnumType
                    or NodeType.StructType
                    or NodeType.RecordType;
    }
}

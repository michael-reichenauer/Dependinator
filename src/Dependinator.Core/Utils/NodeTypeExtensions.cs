using Dependinator.Core.Parsing;

// Note: kept in the globally imported Utils namespace (not Dependinator.Core.Parsing) so the
// extension members are in scope in Dependinator.UI, which only aliases the Parsing namespace.
namespace Dependinator.Core.Utils;

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
                    or NodeType.PropertyMember;

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

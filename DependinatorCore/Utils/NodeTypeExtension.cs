using DependinatorCore.Parsing;

namespace DependinatorCore.Utils;

static class NodeTypeExtension
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
    }
}

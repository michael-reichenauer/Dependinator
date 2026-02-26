using Dependinator.Core.Parsing;

namespace Dependinator.Core.Utils;

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

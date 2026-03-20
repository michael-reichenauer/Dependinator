using Dependinator.Shared.Types;

namespace Dependinator.Modeling.Models;

public record NodeId : Id
{
    public static NodeId Empty = new("");

    private NodeId(string Value)
        : base(Value) { }

    public static NodeId FromName(string name) => new(ToId(name));

    public static NodeId FromId(string id) => new(id);
}

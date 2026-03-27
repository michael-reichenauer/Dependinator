using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Models;

public record LineId : Id
{
    public static LineId Empty = new("");

    private LineId(string Value)
        : base(Value) { }

    public static LineId From(string sourceName, string targetName) => new(Id.ToId($"{sourceName}=>{targetName}"));

    public static LineId FromDirect(string sourceName, string targetName) =>
        new(Id.ToId($"direct:{sourceName}=>{targetName}"));

    public static LineId FromId(string id) => new(id);
}

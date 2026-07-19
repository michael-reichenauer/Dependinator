using System.Text.Json.Serialization;

namespace Dependinator.UI.Modeling.Dtos;

[Serializable]
record LinkDto(string SourceName, string TargetName, string TargetType)
{
    // True for user-drawn links; omitted for parsed links so their serialized form is unchanged.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsManual { get; init; }

    // True when the source type inherits/implements the target type.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsInheritance { get; init; }
}

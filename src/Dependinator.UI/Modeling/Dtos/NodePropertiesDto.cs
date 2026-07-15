using System.Text.Json.Serialization;

namespace Dependinator.UI.Modeling.Dtos;

[Serializable]
record NodePropertiesDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool? IsPrivate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool? IsExecutable { get; init; }
}

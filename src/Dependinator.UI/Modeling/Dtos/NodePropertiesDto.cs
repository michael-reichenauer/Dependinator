using System.Text.Json.Serialization;
using Dependinator.Core.Parsing;

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

    // A member row's signature (see Parsing.NodeProperties.Signature).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Signature { get; init; }

    // Source location so code navigation works right after a cached load, before the
    // background re-parse has refreshed the model. Persisted relative to the model
    // (.sln) folder (see FileSpanPaths) so the cache stays valid when the same solution
    // is opened from a different base path. Omitted for nodes without source (and in
    // caches saved before this field existed, where navigation then needs the re-parse).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public FileSpan? FileSpan { get; init; }

    // Parser-supplied icon (see Parsing.NodeProperties.IconName); omitted for nodes whose icon
    // is the node-type default. Distinct from NodeDto.IconName, the user's custom icon.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? IconName { get; init; }

    // A project's folder (see Parsing.NodeProperties.ProjectFolder), relative to the model's
    // folder like FileSpan: a workspace model nests its sources by it (SourceNesting), also
    // while the project's own source is not parsed again (a refresh of other kinds, a failure).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? ProjectFolder { get; init; }
}

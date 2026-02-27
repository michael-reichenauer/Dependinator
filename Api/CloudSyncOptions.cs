namespace Api;

public sealed class CloudSyncOptions
{
    public const string SectionName = "CloudSync";

    public string ContainerName { get; init; } = "dependinator-models";
    public long MaxUserQuotaBytes { get; init; } = 10 * 1024 * 1024;
    public string? StorageConnectionString { get; init; }
}

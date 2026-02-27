namespace Dependinator.Wasm;

public sealed class CloudSyncClientOptions
{
    public const string SectionName = "CloudSync";

    public string? ApiBaseAddress { get; init; }
    public string LoginPath { get; init; } = "/.auth/login/aad";
    public string LogoutPath { get; init; } = "/.auth/logout";
}

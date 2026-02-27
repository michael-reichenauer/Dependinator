namespace Dependinator.Wasm;

public sealed class CloudSyncClientOptions
{
    public const string SectionName = "CloudSync";

    public bool Enabled { get; init; } = true;
    public string? ApiBaseAddress { get; init; }
    public string LoginPath { get; init; } = "/.auth/login/entraExternalId";
    public string LogoutPath { get; init; } = "/.auth/logout";
}

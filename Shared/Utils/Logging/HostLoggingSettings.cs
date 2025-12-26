namespace Dependinator.Shared.Utils.Logging;

public interface IHostLoggingSettings
{
    bool EnableFileLog { get; }
    bool EnableConsoleLog { get; }
    string? LogFilePath { get; }
}

public record HostLoggingSettings(bool EnableFileLog, bool EnableConsoleLog, string? LogFilePath = null)
    : IHostLoggingSettings;

namespace Dependinator.Core.Utils.Logging;

public interface IHostLoggingSettings
{
    bool EnableFileLog { get; }
    bool EnableConsoleLog { get; }
    string? LogFilePath { get; }
    Action<string>? Output { get; }
}

public record HostLoggingSettings(
    bool EnableFileLog,
    bool EnableConsoleLog,
    string? LogFilePath = null,
    Action<string>? Output = null
) : IHostLoggingSettings;

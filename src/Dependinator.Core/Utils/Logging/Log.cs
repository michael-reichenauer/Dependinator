using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

// Lightweight logging infrastructure for the core: the static Log entry point plus logger
// configuration and host logging settings.
namespace Dependinator.Core.Utils.Logging;

// Log level texts, padded to equal length so log lines align.
static class LogLevels
{
    public const string Debug = "DEBUG";
    public const string Info = "INFO ";
    public const string Warn = "WARN ";
    public const string Error = "ERROR";
    public const string Fatal = "FATAL";
}

// Sentinel parameter that stops positional arguments from accidentally binding to the
// [CallerMemberName]/[CallerFilePath]/[CallerLineNumber] parameters that follow it.
// Empty is null; the type exists only to occupy a parameter slot.
public class StopParameter
{
    public const StopParameter Empty = default;
}

public static class Log
{
    static readonly JsonSerializerOptions JsonOneLine = new() { WriteIndented = false };

    public static void Debug(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        Write(LogLevels.Debug, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    // Info has explicit p1..p4 overloads (instead of a params array) because params
    // cannot be combined with the trailing [Caller...] parameters.
    public static void Info(
        string msg,
        StopParameter stop = StopParameter.Empty,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        Write(LogLevels.Info, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Info(
        string msg,
        object? p1,
        StopParameter stop = StopParameter.Empty,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        Write(LogLevels.Info, $"{msg} {ToText(p1)}", memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Info(
        string msg,
        object? p1,
        object? p2,
        StopParameter stop = StopParameter.Empty,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        Write(LogLevels.Info, $"{msg} {ToText(p1)} {ToText(p2)}", memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Info(
        string msg,
        object? p1,
        object? p2,
        object? p3,
        StopParameter stop = StopParameter.Empty,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        Write(
            LogLevels.Info,
            $"{msg} {ToText(p1)} {ToText(p2)} {ToText(p3)}",
            memberName,
            sourceFilePath,
            sourceLineNumber
        );
    }

    public static void Info(
        string msg,
        object? p1,
        object? p2,
        object? p3,
        object? p4,
        StopParameter stop = StopParameter.Empty,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        Write(
            LogLevels.Info,
            $"{msg} {ToText(p1)} {ToText(p2)} {ToText(p3)} {ToText(p4)}",
            memberName,
            sourceFilePath,
            sourceLineNumber
        );
    }

    public static void Warn(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        Write(LogLevels.Warn, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Error(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        Write(LogLevels.Error, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Exception(
        Exception e,
        string msg = "",
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        Write(LogLevels.Error, msg == "" ? $"{e}" : $"{msg}\n{e}", memberName, sourceFilePath, sourceLineNumber);
    }

    // Hide object.ReferenceEquals from IntelliSense; calling it via Log is always a mistake.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new bool ReferenceEquals(object objA, object objB)
    {
        throw new Exception("Log does not implement ReferenceEquals");
    }

    static void Write(string level, string msg, string memberName, string sourceFilePath, int sourceLineNumber)
    {
        ConfigLogger.Write(level, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    static string ToText(object? obj)
    {
        if (obj == null)
            return "<null>";
        var t = obj.GetType();

        if (
            t.IsPrimitive
            || t == typeof(Decimal)
            || t == typeof(String)
            || t == typeof(DateTime)
            || t == typeof(TimeSpan)
            || t == typeof(Guid)
            || t == typeof(DateTimeOffset)
            || t == typeof(Uri)
            || t == typeof(Version)
            || t == typeof(DBNull)
        )
        {
            return obj.ToString() ?? "";
        }

        if (!Try(out var json, out _, () => JsonSerializer.Serialize(obj, JsonOneLine)))
        {
            return obj.ToString() ?? "";
        }

        return json;
    }
}

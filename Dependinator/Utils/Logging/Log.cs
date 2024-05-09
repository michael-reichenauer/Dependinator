using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Dependinator.Utils.Logging;


static class Log
{
    private static readonly string LevelDebug = "DEBUG";
    private static readonly string LevelInfo = "INFO ";
    private static readonly string LevelWarn = "WARN ";
    private static readonly string LevelError = "ERROR";


    public static void Debug(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelDebug, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Info(
        string msg,
        StopParameter stop = StopParameter.Empty,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelInfo, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Info(
       string msg,
       object p1,
       StopParameter stop = StopParameter.Empty,
       [CallerMemberName] string memberName = "",
       [CallerFilePath] string sourceFilePath = "",
       [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelInfo, $"{msg} {toText(p1)}", memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Info(
      string msg,
      object p1,
      object p2,
      StopParameter stop = StopParameter.Empty,
      [CallerMemberName] string memberName = "",
      [CallerFilePath] string sourceFilePath = "",
      [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelInfo, $"{msg} {toText(p1)} {toText(p2)}", memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Info(
        string msg,
        object p1,
        object p2,
        object p3,
        StopParameter stop = StopParameter.Empty,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelInfo, $"{msg} {toText(p1)} {toText(p2)} {toText(p3)}", memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Info(
        string msg,
        object p1,
        object p2,
        object p3,
        object p4,
        StopParameter stop = StopParameter.Empty,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelInfo, $"{msg} {toText(p1)} {toText(p2)} {toText(p3)} {toText(p4)}", memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Warn(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelWarn, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Error(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelError, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Exception(
        Exception e,
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelError, $"{msg}\n{e}", memberName, sourceFilePath, sourceLineNumber);
    }

    public static void Exception(
        Exception e,
        int stop = 0,  // No used, but needed to separate from other Exception function
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelError, $"{e}", memberName, sourceFilePath, sourceLineNumber);
    }


    // Trying to hide non usable function from code intellisense 
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static new bool ReferenceEquals(object objA, object objB)
    {
        throw new Exception("Assertion does not implement ReferenceEquals, use Ensure or Require");
    }

    static void Write(
        string level,
        string msg,
        string memberName,
        string sourceFilePath,
        int sourceLineNumber)
    {
        ConfigLogger.Write(level, msg, memberName, sourceFilePath, sourceLineNumber);
    }

    static string toText(object obj)
    {
        if (obj == null) return "<null>";
        var t = obj.GetType();

        if (t.IsPrimitive || t == typeof(Decimal) || t == typeof(String) || t == typeof(DateTime) ||
            t == typeof(TimeSpan) || t == typeof(Guid) || t == typeof(DateTimeOffset) ||
            t == typeof(Uri) || t == typeof(Version) || t == typeof(DBNull))
        {
            return obj.ToString() ?? "";
        }

        try
        {
            return obj.ToJson();
        }
        catch
        {
            return obj.ToString() ?? "";
        }
    }

    public class StopParameter
    {
        public const StopParameter Empty = default;
    }
}



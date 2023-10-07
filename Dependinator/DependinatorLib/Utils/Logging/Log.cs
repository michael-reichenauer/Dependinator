using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DependinatorLib.Utils.Logging;


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
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        Write(LevelInfo, msg, memberName, sourceFilePath, sourceLineNumber);
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

    private static void Write(
        string level,
        string msg,
        string memberName,
        string sourceFilePath,
        int sourceLineNumber)
    {
        ConfigLogger.Write(level, msg, memberName, sourceFilePath, sourceLineNumber);
    }
}

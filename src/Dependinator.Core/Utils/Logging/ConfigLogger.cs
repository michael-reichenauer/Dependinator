using System.Runtime.CompilerServices;
using System.Threading.Channels;
using static System.Environment;

namespace Dependinator.Core.Utils.Logging;

// Background log writer: Log queues messages here and a single reader task batches
// them to the configured outputs (file, console and/or a host-provided callback).
public static class ConfigLogger
{
    const string LogFileName = "Dependinator.log";
    const int MaxLogFileSize = 2000000;

    const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    // Length of the formatted timestamp and the space after it, stripped from console
    // output. Must be kept in sync with TimestampFormat.
    const int TimestampPrefixLength = 24;

    record LogMsg(string Level, string Msg, string MemberName, string SourceFilePath, int SourceLineNumber);

    static readonly Channel<LogMsg> logMessages = Channel.CreateUnbounded<LogMsg>(
        new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true,
            AllowSynchronousContinuations = true,
        }
    );

    static readonly ChannelReader<LogMsg> reader = logMessages.Reader;

    static readonly object syncRoot = new object();
    static int prefixLength = 0;
    static string? LogPath;
    static bool isFileLog = false;
    static bool isConsoleLog = false;
    static Action<string>? output = null;

    static readonly TaskCompletionSource doneTask = new TaskCompletionSource();

    static ConfigLogger()
    {
        ProcessLogsAsync().RunInBackground();
        SetPrefixLength();
    }

    // Should be called once at host startup before logging begins; messages logged
    // before configuration may be dropped since all outputs are disabled by default.
    public static void Configure(IHostLoggingSettings settings)
    {
        isFileLog = settings.EnableFileLog;
        isConsoleLog = settings.EnableConsoleLog;
        output = settings.Output;

        if (isFileLog)
        {
            var logPath = string.IsNullOrWhiteSpace(settings.LogFilePath)
                ? Path.Join(GetFolderPath(SpecialFolder.UserProfile), LogFileName)
                : settings.LogFilePath!;
            Init(logPath);
        }
        else
        {
            LogPath = null;
        }
    }

    public static Task CloseAsync()
    {
        try
        {
            logMessages.Writer.TryComplete();
        }
        catch
        {
            // buffer might already be closed in case of crashing
        }

        return doneTask.Task;
    }

    internal static void Write(string level, string msg, string memberName, string sourceFilePath, int sourceLineNumber)
    {
        QueueLogMessage(new LogMsg(level, msg, memberName, sourceFilePath, sourceLineNumber));
    }

    static void Init(string logFilePath)
    {
        LogPath = logFilePath;

        // Start each session with an empty log file; the previous session's content
        // only survives via the ".2.log" rotation in MoveLargeLogFile().
        if (!Try(out var e, () => File.WriteAllText(LogPath, "")))
            throw Asserter.FailFast(e.ErrorMessage);
    }

    // Determines the length of the source path prefix to strip from [CallerFilePath]
    // paths, using this file's own compile-time path. This file is 4 directory levels
    // below the prefix (<prefix>/Dependinator.Core/Utils/Logging/ConfigLogger.cs), so
    // log lines show paths relative to the src/ folder. Update if this file moves.
    static void SetPrefixLength([CallerFilePath] string sourceFilePath = "")
    {
        string rootPath =
            Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(sourceFilePath))))
            ?? "";
        prefixLength = rootPath.Length + 1;
    }

    static void LogDone(
        string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0
    )
    {
        string text = ToLogLine(LogLevels.Info, msg, memberName, sourceFilePath, sourceLineNumber);
        // Bypassing log queue since that is already closed
        WriteToOutputs(new List<string>() { text });
    }

    static async Task ProcessLogsAsync()
    {
        try
        {
            while (await reader.WaitToReadAsync())
            {
                var batchedLines = new List<string>();

                while (reader.TryRead(out LogMsg? l))
                {
                    foreach (var msgLine in l.Msg.Split('\n'))
                    {
                        batchedLines.Add(
                            ToLogLine(l.Level, msgLine, l.MemberName, l.SourceFilePath, l.SourceLineNumber)
                        );
                    }
                }

                WriteToOutputs(batchedLines);
            }

            LogDone("Logging done");
        }
        finally
        {
            doneTask.SetResult();
        }
    }

    static string ToLogLine(string level, string msg, string memberName, string sourceFilePath, int lineNumber)
    {
        string timeStamp = DateTime.Now.ToString(TimestampFormat);

        // Internally queued messages (e.g. from MoveLargeLogFile) have no source path;
        // only strip the prefix from real caller paths.
        int trimLength = prefixLength >= sourceFilePath.Length - 1 ? 0 : prefixLength;
        string filePath = sourceFilePath.Substring(trimLength).Replace(";", "");

        string msgLine = $"{timeStamp} DEP: {level}:\"{msg}\"";
        return $"{msgLine, -100} {{{memberName}() {filePath}:{lineNumber}}}";
    }

    static void QueueLogMessage(LogMsg item)
    {
        try
        {
            logMessages.Writer.TryWrite(item);
        }
        catch
        {
            // Failed to add, the buffer has been closed
        }
    }

    static void WriteToOutputs(IReadOnlyCollection<string> textLines)
    {
        if (!isFileLog && !isConsoleLog && output is null)
        {
            return;
        }

        lock (syncRoot)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    if (output is not null)
                        textLines.ForEach(l => output(l));

                    if (isConsoleLog)
                        Console.WriteLine(
                            textLines
                                .Select(l => l.Length > TimestampPrefixLength ? l[TimestampPrefixLength..] : l)
                                .Join("\n")
                        );
                    if (isFileLog)
                    {
                        if (LogPath == null)
                            return;
                        File.AppendAllLines(LogPath, textLines);

                        long length = new FileInfo(LogPath).Length;

                        if (length > MaxLogFileSize)
                        {
                            MoveLargeLogFile();
                        }
                    }

                    return;
                }
                catch (DirectoryNotFoundException)
                {
                    // Ignore error since folder has been deleted during uninstallation
                    return;
                }
                catch (Exception)
                {
                    Thread.Sleep(30);
                }
            }

            // Retries exhausted; drop the batch since there is no way to report a logging failure.
        }
    }

    static void MoveLargeLogFile()
    {
        try
        {
            if (LogPath == null)
            {
                return;
            }

            string tempPath = LogPath + "." + Guid.NewGuid();
            File.Move(LogPath, tempPath);

            Task.Run(() =>
                {
                    try
                    {
                        string secondLogFile = LogPath + ".2.log";
                        if (File.Exists(secondLogFile))
                        {
                            File.Delete(secondLogFile);
                        }

                        File.Move(tempPath, secondLogFile);
                    }
                    catch (Exception e)
                    {
                        QueueLogMessage(
                            new LogMsg(LogLevels.Fatal, "Failed to move temp to second log file" + e, "", "", 0)
                        );
                    }
                })
                .RunInBackground();
        }
        catch (Exception e)
        {
            QueueLogMessage(new LogMsg(LogLevels.Fatal, "ERROR Failed to move large log file: " + e, "", "", 0));
        }
    }
}

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using static System.Environment;

namespace Dependinator.Utils.Logging;

static class ConfigLogger
{
    static readonly string LogFileName = "Dependinator.log";
    static readonly string LevelInfo = "INFO ";
    static readonly int MaxLogFileSize = 2000000;

    static readonly Channel<string> logTexts = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleWriter = false,
        SingleReader = true,
        AllowSynchronousContinuations = true
    });

    static readonly ChannelReader<string> reader = logTexts.Reader;


    static readonly object syncRoot = new object();
    static int prefixLength = 0;
    static string LogPath = LogFileName;

    static TaskCompletionSource doneTask = new TaskCompletionSource();


    static ConfigLogger()
    {
        Task.Factory.StartNew(ProcessLogsAsync, TaskCreationOptions.LongRunning)
            .RunInBackground();
        // string path = $"{Environment.GetFolderPath(SpecialFolder.UserProfile)}/gmd.log";
        string path = Path.Join(GetFolderPath(SpecialFolder.UserProfile), LogFileName);
        Init(path);
    }

    public static Task CloseAsync()
    {
        try
        {
            logTexts.Writer.TryComplete();
        }
        catch
        {
            // buffer might already be closed in case of crashing
        }

        return doneTask.Task;
    }

    internal static void Write(
       string level,
       string msg,
       string memberName,
       string sourceFilePath,
       int sourceLineNumber)
    {
        var msgLines = msg.Split('\n');
        foreach (var msgLine in msgLines)
        {
            string text = ToLogLine(level, msgLine, memberName, sourceFilePath, sourceLineNumber);
            QueueLogLine(text);
        }
    }


    static void Init(string logFilePath, [CallerFilePath] string sourceFilePath = "")
    {
        LogPath = logFilePath;
        string rootPath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(
            Path.GetDirectoryName(sourceFilePath)))) ?? "";
        prefixLength = rootPath.Length + 1;
        if (!Try(out var e, () => File.WriteAllText(LogPath, ""))) throw Asserter.FailFast(e.ErrorMessage);
    }

    static void LogDone(
       string msg,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        string text = ToLogLine(LevelInfo, msg, memberName, sourceFilePath, sourceLineNumber);
        // Bypassing log queue since that is already closed
        WriteToFile(new List<string>() { text });
    }


    private async static ValueTask ProcessLogsAsync()
    {
        try
        {
            while (await reader.WaitToReadAsync())
            {
                List<string> batchedTexts = new List<string>();

                while (reader.TryRead(out string? logText))
                {
                    batchedTexts.Add(logText);
                }

                try
                {
                    WriteToFile(batchedTexts);
                }
                catch (ThreadAbortException)
                {
                    // The process or app-domain is closing,
                    // Thread.ResetAbort();
                    return;
                }
                catch (Exception e) when (e.IsNotFatal())
                {
                    // Native.OutputDebugString("ERROR Failed to log to file, " + e);
                }
            }

            LogDone("Logging done");
        }
        finally
        {
            doneTask.SetResult();
        }
    }



    static string ToRelativeFilePath(string sourceFilePath)
    {
        return sourceFilePath.Substring(prefixLength).Replace(";", "");
    }


    static string ToLogLine(
        string level,
        string msg,
        string memberName,
        string sourceFilePath,
        int lineNumber)
    {
        string timeStamp = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}";
        int trimLength = prefixLength;
        if (prefixLength >= sourceFilePath.Length - 1)
        {
            trimLength = 0;
        }

        string filePath = sourceFilePath.Substring(trimLength).Replace(";", "");

        int classStartIndex = filePath.LastIndexOf(Path.DirectorySeparatorChar);
        if (classStartIndex == -1)
        {
            classStartIndex = 0;
        }
        int extensionIndex = filePath.LastIndexOf('.');
        if (extensionIndex == -1)
        {
            extensionIndex = filePath.Length - 1;
        }
        string className = filePath.Substring(classStartIndex + 1, extensionIndex - classStartIndex - 1);
        string msgLine = $"{timeStamp} {level}:\"{msg}\"";

        string line = $"{msgLine,-100} {{{memberName}() {filePath}:{lineNumber}}}";
        return line;
    }


    private static void QueueLogLine(string text)
    {
        try
        {
            logTexts.Writer.TryWrite(text);
        }
        catch
        {
            // Failed to add, the buffer has been closed
        }
    }


    private static void WriteToFile(IReadOnlyCollection<string> text)
    {
        if (LogPath == null)
        {
            return;
        }

        Exception? error = null;
        lock (syncRoot)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    File.AppendAllLines(LogPath, text);

                    long length = new FileInfo(LogPath).Length;

                    if (length > MaxLogFileSize)
                    {
                        MoveLargeLogFile();
                    }

                    return;
                }
                catch (DirectoryNotFoundException)
                {
                    // Ignore error since folder has been deleted during uninstallation
                    return;
                }
                catch (ThreadAbortException)
                {
                    // Process or app-domain is closing
                    // Thread.ResetAbort();
                    return;
                }
                catch (Exception e)
                {
                    Thread.Sleep(30);
                    error = e;
                }
            }
        }

        if (error != null)
        {
            throw error;
        }
    }

    private static void MoveLargeLogFile()
    {
        try
        {
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
                    QueueLogLine("ERROR Failed to move temp to second log file: " + e);
                }

            }).RunInBackground();
        }
        catch (Exception e)
        {
            QueueLogLine("ERROR Failed to move large log file: " + e);
        }
    }
}

﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;


namespace Dependinator.Utils
{
    internal static class Log
    {
        private static readonly int MaxLogFileSize = 2000000;
        private static readonly BlockingCollection<string> logTexts = new BlockingCollection<string>();

        private static readonly object syncRoot = new object();

        private static readonly int ProcessID = Process.GetCurrentProcess().Id;
        private static readonly string LevelUsage = "USAGE";
        private static readonly string LevelDebug = "DEBUG";
        private static readonly string LevelInfo = "INFO ";
        private static readonly string LevelWarn = "WARN ";
        private static readonly string LevelError = "ERROR";

        private static int prefixLength;
        private static string LogPath;


        static Log()
        {
            Task.Factory.StartNew(SendBufferedLogRows, TaskCreationOptions.LongRunning)
                .RunInBackground();
        }


        public static void Init(string logFilePath, [CallerFilePath] string sourceFilePath = "")
        {
            LogPath = logFilePath;
            string rootPath = Path.GetDirectoryName(Path.GetDirectoryName(sourceFilePath));
            prefixLength = rootPath.Length + 1;
        }


        private static void SendBufferedLogRows()
        {
            while (!logTexts.IsCompleted)
            {
                List<string> batchedTexts = new List<string>();
                // Wait for texts to log
                string filePrefix = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss,fff} [{ProcessID}]";
                string logText = logTexts.Take();
                OutputDebugString(logText);
                batchedTexts.Add($"{filePrefix} {logText}");

                // Check if there might be more buffered log texts, if so add them in batch
                while (logTexts.TryTake(out logText))
                {
                    OutputDebugString(logText);
                    batchedTexts.Add($"{filePrefix} {logText}");
                }

                try
                {
                    WriteToFile(string.Join("\n", batchedTexts) + "\n");
                }
                catch (ThreadAbortException)
                {
                    // The process or app-domain is closing,
                    Thread.ResetAbort();
                    return;
                }
                catch (Exception e) when (e.IsNotFatal())
                {
                    OutputDebugString("ERROR Failed to log to file, " + e);
                }
            }
        }


        private static void OutputDebugString(string logText)
        {
            const int maxLength = 1000;
            int totalLength = logText.Length;

            if (totalLength < maxLength)
            {
                Native.OutputDebugString(logText);
                return;
            }

            // Seems that OutputDebugString has some max size
            int index = 0;
            int length = maxLength;
            while (index < totalLength)
            {
                Native.OutputDebugString(logText.Substring(index, length));
                index += length;
                length = Math.Min(maxLength, totalLength - index);
            }
        }


        public static void Usage(
            string msg,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Write(LevelUsage, msg, memberName, sourceFilePath, sourceLineNumber);
        }


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
            Track.Exception(e, msg);
        }


        public static void Exception(
            Exception e,
            DelimiterParameter stop = default(DelimiterParameter),
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            Write(LevelError, $"{e}", memberName, sourceFilePath, sourceLineNumber);
            Track.Exception(e, "");
        }


        private static void Write(
            string level,
            string msg,
            string memberName,
            string filePath,
            int lineNumber)
        {
            filePath = filePath.Substring(prefixLength);
            string text = $"{level} [{ProcessID}] {filePath}({lineNumber}) {memberName} - {msg}";

            //Native.OutputDebugString(text);

            if (level == LevelUsage || level == LevelWarn || level == LevelError)
            {
                //SendUsage(text);
            }

            try
            {
                SendLog(text);
            }
            catch (Exception e) when (e.IsNotFatal())
            {
                SendLog("ERROR Failed to log " + e);
            }
        }


        private static void SendLog(string text)
        {
            logTexts.Add(text);
        }


        private static void WriteToFile(string text)
        {
            if (LogPath == null)
            {
                return;
            }

            Exception error = null;
            lock (syncRoot)
            {
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        File.AppendAllText(LogPath, text);

                        MoveIfLogToLarge();

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
                        Thread.ResetAbort();
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


        private static void MoveIfLogToLarge()
        {
            try
            {
                long length = new FileInfo(LogPath).Length;

                if (length > MaxLogFileSize)
                {
                    MoveLargeLogFile();
                }
            }
            catch (Exception)
            {
                // Ignore large file for now
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
                        SendLog("ERROR Failed to move temp to second log file: " + e);
                    }
                }).RunInBackground();
            }
            catch (Exception e)
            {
                SendLog("ERROR Failed to move large log file: " + e);
            }
        }


        private static class Native
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
            public static extern void OutputDebugString(string message);
        }


        public struct DelimiterParameter
        {
        }
    }
}

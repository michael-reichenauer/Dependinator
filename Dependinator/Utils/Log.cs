using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dependinator.Common.SettingsHandling;


namespace Dependinator.Utils
{
	internal static class Log
	{
		private static readonly int MaxLogFileSize = 2000000;

		private static readonly UdpClient UdpClient = new UdpClient();
		private static readonly BlockingCollection<string> logTexts = new BlockingCollection<string>();

		private static readonly IPEndPoint usageLogEndPoint =
			new IPEndPoint(IPAddress.Parse("10.85.12.4"), 41110);

		private static readonly object syncRoot = new object();

		private static readonly string LogPath = ProgramPaths.GetLogFilePath();
		private static readonly int ProcessID = Process.GetCurrentProcess().Id;
		private static readonly string LevelUsage = "USAGE";
		private static readonly string LevelDebug = "DEBUG";
		private static readonly string LevelInfo = "INFO ";
		private static readonly string LevelWarn = "WARN ";
		private static readonly string LevelError = "ERROR";
		private static readonly Lazy<bool> DisableErrorAndUsageReporting;
		private static readonly int prefixLength = 0;

		static Log()
		{
			DisableErrorAndUsageReporting = new Lazy<bool>(() =>
				Settings.Get<Options>().DisableErrorAndUsageReporting);

			Task.Factory.StartNew(SendBufferedLogRows, TaskCreationOptions.LongRunning)
				.RunInBackground();

			prefixLength = GetSourceFilePrefixLength();
		}


		private static int GetSourceFilePrefixLength([CallerFilePath] string sourceFilePath = "")
		{
			return sourceFilePath.IndexOf("Dependinator\\Utils\\Log.cs");
		}


		private static void SendBufferedLogRows()
		{
			List<string> batchedTexts = new List<string>();
			while (!logTexts.IsCompleted)
			{
				// Wait for texts to log
				string filePrefix = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss,fff} [{ProcessID}]";
				string logText = logTexts.Take();
				Native.OutputDebugString(logText);
				batchedTexts.Add($"{filePrefix} {logText}");

				// Check if there might be more buffered log texts, if so add them in batch
				while (logTexts.TryTake(out logText))
				{
					Native.OutputDebugString(logText);
					batchedTexts.Add($"{filePrefix} {logText}");
				}

				try
				{
					WriteToFile(batchedTexts);
				}
				catch (ThreadAbortException)
				{
					// The process or app-domain is closing,
					Thread.ResetAbort();
					return;
				}
				catch (Exception e) when (e.IsNotFatal())
				{
					Native.OutputDebugString("ERROR Failed to log to file, " + e);
				}
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



		private static void SendUsage(string text)
		{
			if (DisableErrorAndUsageReporting.Value)
			{
				return;
			}

			try
			{
				string logRow = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss,fff} [{ProcessID}] {text}";

				byte[] bytes = System.Text.Encoding.UTF8.GetBytes(logRow);
				UdpClient.Send(bytes, bytes.Length, usageLogEndPoint);
			}
			catch (Exception)
			{
				// Ignore failed
			}
		}


		private static void WriteToFile(IReadOnlyCollection<string> text)
		{
			Exception error = null;
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
	}
}
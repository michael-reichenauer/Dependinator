using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


namespace DependinatorVse.Commands.Private
{
	internal static class Log
	{
		public static void Debug(
			string msg,
			[CallerMemberName] string memberName = "",
			[CallerFilePath] string sourceFilePath = "",
			[CallerLineNumber] int sourceLineNumber = 0)
		{
			Write("DEBUG", msg, memberName, sourceFilePath, sourceLineNumber);
		}


		public static void Warn(
			string msg,
			[CallerMemberName] string memberName = "",
			[CallerFilePath] string sourceFilePath = "",
			[CallerLineNumber] int sourceLineNumber = 0)
		{
			Write("WARN", msg, memberName, sourceFilePath, sourceLineNumber);
		}


		public static void Error(
			string msg,
			[CallerMemberName] string memberName = "",
			[CallerFilePath] string sourceFilePath = "",
			[CallerLineNumber] int sourceLineNumber = 0)
		{
			Write("ERROR", msg, memberName, sourceFilePath, sourceLineNumber);
		}


		private static void Write(string level, string msg, string member, string fileName, int lineNumber)
		{
			try
			{
				Native.OutputDebugString($"{level} {fileName}({lineNumber}) {member} - {msg}");
			}
			catch (Exception)
			{
				// ignore logging errors
			}
		}


		private static class Native
		{
			[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
			public static extern void OutputDebugString(string message);
		}
	}
}
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;


namespace Dependinator.Utils.Threading
{
    public class Timing
    {
        private readonly Stopwatch stopwatch;
        private int count;
        private TimeSpan lastTimeSpan = TimeSpan.Zero;


        public Timing()
        {
            stopwatch = new Stopwatch();
            stopwatch.Start();
        }


        public TimeSpan Elapsed
        {
            get
            {
                lastTimeSpan = stopwatch.Elapsed;
                return lastTimeSpan;
            }
        }

        public long ElapsedMs => (long)Elapsed.TotalMilliseconds;

        public TimeSpan Diff
        {
            get
            {
                TimeSpan previous = lastTimeSpan;
                return Elapsed - previous;
            }
        }

        public long DiffMs => (long)Diff.TotalMilliseconds;

        public static Timing Start() => new Timing();


        public TimeSpan Stop()
        {
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }


        public void Log(
            string message,
            DelimiterParameter delimiterParameter = default(DelimiterParameter),
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            count++;

            Utils.Log.Debug(
                $"{count}: {message}: {this}", memberName, sourceFilePath, sourceLineNumber);
        }


        public void Log(
            DelimiterParameter delimiterParameter = default(DelimiterParameter),
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            count++;

            Utils.Log.Debug($"At {count}: {this}", memberName, sourceFilePath, sourceLineNumber);
        }


        public override string ToString() => $"Timing: {DiffMs}ms ({ElapsedMs}ms)";


        public struct DelimiterParameter
        {
        }
    }
}

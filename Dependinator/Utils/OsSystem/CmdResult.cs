using System.Collections.Generic;


namespace Dependinator.Utils.OsSystem
{
    public class CmdResult
    {
        public CmdResult(int exitCode, IReadOnlyList<string> output, IReadOnlyList<string> error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }


        public int ExitCode { get; }
        public IReadOnlyList<string> Output { get; }
        public IReadOnlyList<string> Error { get; }


        public override string ToString()
        {
            return $"Exit code: {ExitCode}";
        }
    }
}

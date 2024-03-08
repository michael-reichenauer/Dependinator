
using System.Runtime.InteropServices;

namespace Dependinator.Utils
{
    public static class Build
    {
        public static string Version => typeof(Build).Assembly.GetName().Version!.ToString();

        public static bool IsWebAssembly => RuntimeInformation.ProcessArchitecture == Architecture.Wasm;
    }
}
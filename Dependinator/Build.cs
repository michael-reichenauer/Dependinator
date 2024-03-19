
using System.Runtime.InteropServices;

namespace Dependinator;


public static class Build
{
    public static string Version => typeof(Build).Assembly.GetName().Version!.ToString();

    public static bool IsWebAssembly => RuntimeInformation.ProcessArchitecture == Architecture.Wasm;
}

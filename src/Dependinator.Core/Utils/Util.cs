using System.Runtime.CompilerServices;

// General-purpose utility helpers used across the core: the Result (R/R<T>) error-handling
// types, async and threading helpers, common extension methods, timing/logging aids, and
// dependency-injection support.
namespace Dependinator.Core.Utils;

public static class Util
{
    public static string CurrentFilePath([CallerFilePath] string sourceFilePath = "") => sourceFilePath;
}

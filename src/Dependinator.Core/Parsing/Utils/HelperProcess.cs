using System.Diagnostics;
using System.Text;

namespace Dependinator.Core.Parsing.Utils;

// Runs a parser helper: a console program in the language it parses (the TypeScript parser on
// Node, ...), which prints the model items to stdout, its log to stderr, and exits with 0. The
// way the VS Code extension runs the language server, one level down.
static class HelperProcess
{
    // Runs the program to its end and returns what it printed to stdout. A failure is the
    // last line it printed to stderr.
    public static async Task<Result<string>> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout
    )
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        arguments.ForEach(startInfo.ArgumentList.Add);
        foreach (var (variable, value) in environment)
            startInfo.Environment[variable] = value;

        var name = Path.GetFileName(fileName);
        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception e)
        {
            return new Error($"Could not start {name}.", e);
        }
        if (process is null)
            return new Error($"Could not start {name}.");

        using (process)
        {
            process.StandardInput.Close();
            // Both streams are read while the program runs: a full pipe would block it.
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();

            using var timeoutSource = new CancellationTokenSource(timeout);
            try
            {
                await process.WaitForExitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException)
            {
                Result.Catch(() => process.Kill(entireProcessTree: true));
                return new Error($"{name} did not finish within {timeout.TotalMinutes:0} minutes.");
            }

            var errorLines = (await errors).Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
            errorLines.ForEach(line => Log.Info($"{name}: {line}"));
            if (process.ExitCode != 0)
                return new Error(errorLines.LastOrDefault() ?? $"{name} failed with exit code {process.ExitCode}.");

            return await output;
        }
    }
}

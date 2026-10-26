using System.ComponentModel;
using System.Diagnostics;

namespace Dependinator.Claude;

// How many files have uncommitted changes in a folder's git repository (`git status --porcelain`),
// so the UI can warn before Claude Code's edits mix with the user's own.
static class GitStatus
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public static async Task<Result<int>> CountUncommittedChangesAsync(string folder)
    {
        if (!Directory.Exists(folder))
            return new Error($"The folder was not found: {folder}");
        var info = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = folder,
        };
        foreach (var argument in new[] { "status", "--porcelain" })
            info.ArgumentList.Add(argument);

        Process process;
        try
        {
            process = Process.Start(info) ?? throw new Win32Exception("No process");
        }
        catch (Win32Exception e)
        {
            return new Error("git was not found.", e);
        }
        using (process)
        {
            using var timeout = new CancellationTokenSource(Timeout);
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var errors = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
                if (process.ExitCode != 0)
                    return new Error($"Not a git repository: {(await errors).Trim()}");
                return CountLines(await output);
            }
            catch (OperationCanceledException)
            {
                Result.Catch(() => process.Kill(entireProcessTree: true));
                return new Error("git status did not answer in time.");
            }
        }
    }

    internal static int CountLines(string porcelain) =>
        porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
}

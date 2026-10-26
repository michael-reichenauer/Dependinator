using System.ComponentModel;
using System.Diagnostics;

namespace Dependinator.Claude;

// How many files have uncommitted changes in a folder's git repository (`git status --porcelain`,
// the whole repository, since Claude Code's edits are reviewed there), so the UI can warn before
// they mix with the user's own.
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
            return new Error("git was not found, so uncommitted changes could not be checked.", e);
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
                {
                    var message = (await errors).Trim();
                    return message.Contains("not a git repository", StringComparison.OrdinalIgnoreCase)
                        ? new Error("The model's folder is not in a git repository.")
                        : new Error($"git status failed: {FirstLine(message)}");
                }
                return CountLines(await output);
            }
            catch (OperationCanceledException)
            {
                Result.Catch(() => process.Kill(entireProcessTree: true));
                return new Error("git status did not answer in time.");
            }
        }
    }

    static string FirstLine(string text) => text.Split('\n', 2)[0].Trim();

    internal static int CountLines(string porcelain) =>
        porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
}

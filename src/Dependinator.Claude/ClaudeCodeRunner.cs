using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace Dependinator.Claude;

// Runs Claude Code in print mode (`claude -p`) for one structured answer: the prompt goes in on
// stdin, the schema as --json-schema, and the answer comes back in the JSON result's
// structured_output. No tools (the prompt carries the code it needs), no saved session, and a
// temporary working folder, so the workspace's own Claude settings, hooks and CLAUDE.md are not
// loaded. It runs with the user's Claude login and their default model.
static class ClaudeCodeRunner
{
    static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    public static async Task<Result<string>> AskAsync(string prompt, string jsonSchema)
    {
        var schema = Result.Catch(() => JsonDocument.Parse(jsonSchema).RootElement.GetRawText());
        if (schema is not string compactSchema)
            return new Error("The answer schema is not valid JSON.", schema.Error);

        var started = Start(compactSchema);
        if (started is not Process process)
            return started.Error;
        using (process)
        {
            using var timeout = new CancellationTokenSource(Timeout);
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var errors = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.StandardInput.WriteAsync(prompt.AsMemory(), timeout.Token);
                process.StandardInput.Close();
                await process.WaitForExitAsync(timeout.Token);
                var result = ParseOutput(await output, await errors, process.ExitCode);
                Log.Info("Claude Code answered", result is string);
                return result;
            }
            catch (OperationCanceledException)
            {
                Result.Catch(() => process.Kill(entireProcessTree: true));
                return new Error("Claude Code did not answer within 5 minutes.");
            }
            catch (IOException e)
            {
                return new Error("Claude Code stopped before it answered.", e);
            }
        }
    }

    // The command where the PATH has it, else where Anthropic's installers put it: VS Code started
    // from the dock (macOS) or a desktop launcher often has a shorter PATH than a terminal.
    static Result<Process> Start(string schema)
    {
        Exception? notFound = null;
        foreach (var command in Commands())
        {
            var info = new ProcessStartInfo(command)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetTempPath(),
            };
            foreach (var argument in Arguments(schema))
                info.ArgumentList.Add(argument);
            try
            {
                return Process.Start(info) ?? throw new Win32Exception("No process");
            }
            catch (Win32Exception e)
            {
                notFound = e;
            }
        }
        return new Error(
            "Claude Code (the claude command) was not found. Install it from claude.com/claude-code and sign in "
                + "with \"claude\" in a terminal once.",
            notFound!
        );
    }

    internal static IReadOnlyList<string> Arguments(string schema) =>
        ["-p", "--output-format", "json", "--json-schema", schema, "--tools", "", "--no-session-persistence"];

    static IEnumerable<string> Commands()
    {
        yield return "claude";
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(home, ".local", "bin", "claude.exe");
            yield break;
        }
        yield return Path.Combine(home, ".local", "bin", "claude");
        yield return Path.Combine(home, ".claude", "local", "claude");
        yield return "/opt/homebrew/bin/claude";
        yield return "/usr/local/bin/claude";
    }

    // The answer in the result JSON that --output-format json prints: the structured output as
    // JSON text, or what went wrong (not signed in, say) as an error.
    internal static Result<string> ParseOutput(string output, string errors, int exitCode)
    {
        var parsed = Result.Catch(() => JsonDocument.Parse(output));
        if (parsed is not JsonDocument document)
        {
            var message = FirstLine(errors) ?? FirstLine(output) ?? $"exit code {exitCode}";
            return new Error($"Claude Code could not answer: {message}");
        }
        using (document)
        {
            var root = document.RootElement;
            var text =
                root.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.String
                    ? result.GetString()
                    : null;
            if (root.TryGetProperty("is_error", out var isError) && isError.ValueKind == JsonValueKind.True)
                return new Error($"Claude Code could not answer: {FirstLine(text) ?? "an unknown error"}");
            if (
                root.TryGetProperty("structured_output", out var structured)
                && structured.ValueKind == JsonValueKind.Object
            )
                return structured.GetRawText();
            return text is { Length: > 0 } && text.TrimStart().StartsWith('{')
                ? text.Trim()
                : new Error("Claude Code gave no answer in the expected format. Please try again.");
        }
    }

    static string? FirstLine(string? text) =>
        text?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
}

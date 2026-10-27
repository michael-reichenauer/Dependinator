using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Dependinator.Core.ModelQueries;

namespace Dependinator.Claude;

// Runs Claude Code in print mode (`claude -p`) for one structured answer: the prompt goes in on
// stdin, the schema as --json-schema, and the answer comes back in the JSON result's
// structured_output. It runs with the user's Claude login and their default model, and saves no
// session. In the code's folder it may read and search the files there (Read, Grep, Glob), to
// check what the prompt's excerpts leave out: no tool that changes files or runs commands, and
// reads outside the folder need a permission that print mode cannot ask for, so they are denied.
// Only the user's own Claude settings load (not the folder's, whose hooks could run commands), and
// no MCP servers of the user's. Without a folder (the demo model, say) it gets no tools and a new private
// temporary folder, so nothing planted in a shared temp folder is loaded. FixAsync also lets it
// edit and create files in the folder (Edit, Write, accepted without asking), still with no
// command tools, so it cannot build or run tests; edits outside the folder are denied too. Given
// the address of Dependinator's own MCP server (model tools), an ask may also use its read-only
// tools on the open diagram's model: that server only, from a config file in a private folder,
// since its token would show in the process list on the command line.

static class ClaudeCodeRunner
{
    static readonly TimeSpan AskTimeout = TimeSpan.FromMinutes(5);

    // Improving the comments of a namespace or two can take long; the user can stop it any time.
    static readonly TimeSpan FixTimeout = TimeSpan.FromMinutes(30);

    // The tools Claude Code may use in the code's folder: reading and searching only.
    const string ReadTools = "Read,Grep,Glob";

    // Without this, Claude Code tends to answer from the prompt's excerpts alone ("I did not read
    // the code"), although the point of this route is that it can check them.
    const string ReadCodeInstruction =
        "The code the prompt is about is in the working directory, and you can read and search it with the Read, "
        + "Grep and Glob tools. Before you answer, read the files behind the types and links the prompt names, to "
        + "check what the excerpts leave out. You cannot change files or run commands; that is intended.";

    // With the model tools the prompt is about the model, and the code is there to look things up
    // in, not to check excerpts against.
    const string BrowseCodeInstruction =
        "The code is in the working directory, and you can read and search it with the Read, Grep and Glob tools "
        + "where the prompt leaves something open. You cannot change files or run commands; that is intended.";

    // The tools for a fix: reading and searching, and editing and creating files in the folder.
    const string FixTools = "Read,Grep,Glob,Edit,Write";

    // The fix prompt asks to build and run the tests, which needs commands this route leaves out.
    const string FixCodeInstruction =
        "The code the prompt is about is in the working directory. You can read, search, edit and create files "
        + "there (Read, Grep, Glob, Edit, Write), but not run commands, so you cannot build or run the tests: skip "
        + "those steps, and say in your summary what the user should build and test. Change only what the task "
        + "needs, and nothing outside the working directory. If an assumption does not hold and you cannot make "
        + "the change safely, change nothing and say why. List every file you changed or created.";

    // The MCP server's name in the config, which names its tools: mcp__dependinator__list_rules.
    const string ModelToolsServerName = "dependinator";

    const string ModelToolsInstruction =
        "You can also ask Dependinator, which shows this code as a dependency model, through its tools "
        + "(mcp__dependinator__...): get_model, list_children, search_nodes, get_dependencies, find_paths, "
        + "list_cycles, list_rules and check_rule. They answer on the model the user has open, so use them for "
        + "what the prompt leaves out.";

    public enum Mode
    {
        NoTools,
        ReadCode,
        FixCode,
    }

    public static async Task<Result<string>> AskAsync(
        string prompt,
        string jsonSchema,
        string? codeFolder,
        ModelToolsAddress? modelTools = null
    )
    {
        var schema = Result.Catch(() => JsonDocument.Parse(jsonSchema).RootElement.GetRawText());
        if (schema is not string compactSchema)
            return new Error("The answer schema is not valid JSON.", schema.Error);

        var hasCodeFolder = IsFolder(codeFolder);
        if (hasCodeFolder && modelTools is null)
            return await RunAsync(prompt, Arguments(compactSchema, Mode.ReadCode), codeFolder!, AskTimeout, default);

        // A private folder for the MCP config, and the working folder when there is no code folder.
        var folder = Result.Catch(() => Directory.CreateTempSubdirectory("dependinator-claude-"));
        if (folder is not DirectoryInfo privateFolder)
            return new Error("Could not create a folder for Claude Code.", folder.Error);
        try
        {
            string? mcpConfig = null;
            if (modelTools is not null)
            {
                mcpConfig = Path.Combine(privateFolder.FullName, "mcp.json");
                if (Result.Catch(() => File.WriteAllText(mcpConfig, McpConfig(modelTools))) is Error e)
                    return new Error("Could not write Claude Code's MCP config.", e);
            }

            return hasCodeFolder
                ? await RunAsync(
                    prompt,
                    Arguments(compactSchema, Mode.ReadCode, mcpConfig),
                    codeFolder!,
                    AskTimeout,
                    default
                )
                : await RunAsync(
                    prompt,
                    Arguments(compactSchema, Mode.NoTools, mcpConfig),
                    privateFolder.FullName,
                    AskTimeout,
                    default
                );
        }
        finally
        {
            Result.Catch(() => privateFolder.Delete(recursive: true));
        }
    }

    // Claude Code's MCP config with Dependinator's server only.
    internal static string McpConfig(ModelToolsAddress modelTools)
    {
        var server = new Dictionary<string, object> { ["type"] = "http", ["url"] = modelTools.Url };
        if (modelTools.Token is { Length: > 0 } token)
            server["headers"] = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" };
        return JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                ["mcpServers"] = new Dictionary<string, object> { [ModelToolsServerName] = server },
            }
        );
    }

    // Lets Claude Code change the code in codeFolder as the prompt asks; cancelled by ct (the user).
    public static async Task<Result<string>> FixAsync(
        string prompt,
        string jsonSchema,
        string codeFolder,
        CancellationToken ct
    )
    {
        var schema = Result.Catch(() => JsonDocument.Parse(jsonSchema).RootElement.GetRawText());
        if (schema is not string compactSchema)
            return new Error("The answer schema is not valid JSON.", schema.Error);
        if (!IsFolder(codeFolder))
            return new Error($"The code's folder was not found: {codeFolder}");
        return await RunAsync(prompt, Arguments(compactSchema, Mode.FixCode), codeFolder, FixTimeout, ct);
    }

    static bool IsFolder(string? path) =>
        path is { Length: > 0 } && Path.IsPathFullyQualified(path) && Directory.Exists(path);

    static async Task<Result<string>> RunAsync(
        string prompt,
        IReadOnlyList<string> arguments,
        string workingFolder,
        TimeSpan timeLimit,
        CancellationToken ct
    )
    {
        var started = Start(arguments, workingFolder);
        if (started is not Process process)
            return started.Error;
        using (process)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(timeLimit);
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var errors = process.StandardError.ReadToEndAsync(timeout.Token);
                try
                {
                    await process.StandardInput.WriteAsync(prompt.AsMemory(), timeout.Token);
                    process.StandardInput.Close();
                }
                catch (IOException)
                {
                    // Claude Code exited before it read the prompt (not signed in, say): what it
                    // printed says why.
                }
                await process.WaitForExitAsync(timeout.Token);
                var result = ParseOutput(await output, await errors, process.ExitCode);
                Log.Info($"Claude Code answered {result is string} in {workingFolder}");
                return result;
            }
            catch (OperationCanceledException)
            {
                Result.Catch(() => process.Kill(entireProcessTree: true));
                return ct.IsCancellationRequested
                    ? new Error("Claude Code was stopped.")
                    : new Error($"Claude Code did not answer within {timeLimit.TotalMinutes:0} minutes.");
            }
            catch (IOException e)
            {
                return new Error("Claude Code stopped before it answered.", e);
            }
        }
    }

    // The command where the PATH has it, else where Anthropic's installers put it: VS Code started
    // from the dock (macOS) or a desktop launcher often has a shorter PATH than a terminal.
    static Result<Process> Start(IReadOnlyList<string> arguments, string workingFolder)
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
                WorkingDirectory = workingFolder,
            };
            foreach (var argument in arguments)
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
            "Claude Code (the claude command) was not found. Install it from claude.com/claude-code (on Windows the "
                + "native installer, whose claude.exe can be started directly) and sign in with \"claude\" in a "
                + "terminal once.",
            notFound!
        );
    }

    // With an MCP config (only for an ask), the model tools come on top: that server alone, its
    // tools allowed without asking, and a word on them in the system prompt. Every run loads only
    // the MCP servers it is given, none of the user's own.
    internal static IReadOnlyList<string> Arguments(string schema, Mode mode, string? mcpConfig = null)
    {
        var withModelTools = mcpConfig is not null && mode is not Mode.FixCode;
        List<string> arguments = ["-p", "--output-format", "json", "--json-schema", schema];
        switch (mode)
        {
            case Mode.NoTools:
                arguments.AddRange(["--tools", ""]);
                break;
            case Mode.ReadCode:
                arguments.AddRange(["--tools", ReadTools, "--setting-sources", "user"]);
                break;
            case Mode.FixCode:
                arguments.AddRange([
                    "--tools",
                    FixTools,
                    "--permission-mode",
                    "acceptEdits",
                    "--setting-sources",
                    "user",
                ]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown Claude Code mode");
        }
        if (withModelTools)
            arguments.AddRange(["--mcp-config", mcpConfig!, "--allowedTools", $"mcp__{ModelToolsServerName}"]);
        arguments.Add("--strict-mcp-config");

        string[] instructions = (mode, withModelTools) switch
        {
            (Mode.ReadCode, false) => [ReadCodeInstruction],
            (Mode.ReadCode, true) => [BrowseCodeInstruction, ModelToolsInstruction],
            (Mode.FixCode, _) => [FixCodeInstruction],
            (_, true) => [ModelToolsInstruction],
            _ => [],
        };
        if (instructions.Length > 0)
            arguments.AddRange(["--append-system-prompt", string.Join(" ", instructions)]);
        arguments.Add("--no-session-persistence");
        return arguments;
    }

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

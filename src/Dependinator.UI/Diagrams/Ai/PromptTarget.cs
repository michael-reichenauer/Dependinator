namespace Dependinator.UI.Diagrams.Ai;

// Who a prompt that changes the code is for, which decides what it carries. The routes that run
// from the app (Claude Code, Copilot's agent) work in the user's own folder with its tools, and a
// copied prompt goes where Settings › AI says (AiChatPage): a coding agent with its own copy of the
// repository (a Claude Code project, say), or an AI chat that sees only the prompt.
public enum PromptTarget
{
    // Claude Code or Copilot's agent in the user's folder: it reads the code and makes the change.
    LocalAgent,

    // An agent with its own copy of the repository: it may lack the user's local changes, has no
    // Dependinator MCP server, and hands its change back as a branch and a pull request.
    CodingAgent,

    // An AI chat without the code: the prompt carries the code, and the answer the edits.
    Chat,
}

public static class PromptTargets
{
    // What the note under a copied prompt that changes code says, by where it goes: "The fix
    // prompt is copied. …", then what to do, then what a re-parse shows (result).
    public static string CopiedNote(PromptTarget target, string result, string prompt = "The fix prompt") =>
        target switch
        {
            PromptTarget.Chat => $"{prompt} is copied, with the code to change. Paste it into your AI chat and apply "
                + $"the edits it gives back, then re-parse: {result}",
            PromptTarget.CodingAgent => $"{prompt} is copied. Paste it into your coding agent: it makes the change on "
                + $"a branch and opens a pull request. With that change here, re-parse: {result}",
            _ => $"{prompt} is copied. Paste it into Claude Code, or another AI that can change the code. When the "
                + $"change is made, re-parse: {result}",
        };
}

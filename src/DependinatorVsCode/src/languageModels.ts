import * as vscode from "vscode";
import type { LanguageClient } from "vscode-languageclient/node";
import * as logger from "./logger";

// The language models VS Code offers to extensions (vscode.lm), such as GitHub Copilot's, as an AI
// provider for the AI help in the diagram (the "Copilot" provider in Settings › AI). Dependinator.Lsp
// forwards the UI's requests here (Dependinator.Lsp/VsCodeModels); the extension lists the models
// and asks one. The first request shows VS Code's own consent dialog, which is why the UI asks only
// when the user clicked a button.

// LSP protocol methods shared with Dependinator.Lsp/VsCodeModels.
const modelsRequestMethod = "dependinator/lm/models";
const askRequestMethod = "dependinator/lm/ask";
const turnRequestMethod = "dependinator/lm/turn";
const openChatRequestMethod = "dependinator/lm/openChat";

// The name of the tool the model must call with its answer: the vscode.lm API has no structured
// output, so the answer's JSON schema is the tool's input schema instead.
const answerToolName = "answer";

// Longer than any answer should take, so a hung request does not keep the dialog spinning.
const askTimeoutMs = 5 * 60 * 1000;

interface ModelInfo {
    id: string;
    name: string;
    vendor: string;
    family: string;
    maxInputTokens: number;
}

interface AskParams {
    prompt: string;
    jsonSchema: string;
    modelId: string | null;
}

// One model turn of the LSP's tool loop (Dependinator.Lsp/VsCodeModels/VsCodeToolsAgent.cs): the
// conversation so far, the model tools it may call (Dependinator's MCP tools; the LSP runs them),
// and the answer's schema. Tool inputs travel as JSON text.
interface TurnPart {
    kind: "text" | "toolCall" | "toolResult";
    text?: string | null;
    callId?: string | null;
    name?: string | null;
    input?: string | null;
}

interface TurnMessage {
    role: "user" | "assistant";
    parts: TurnPart[];
}

interface TurnTool {
    name: string;
    description: string;
    inputSchema: string;
}

interface TurnParams {
    modelId: string | null;
    messages: TurnMessage[];
    tools: TurnTool[];
    answerSchema: string;
    isLastTurn: boolean;
}

interface TurnResult {
    parts: TurnPart[] | null;
    error: string | null;
}

interface OpenChatParams {
    prompt: string;
}

// The chat answers in its own view; the command may not return until the agent is done, so the
// hand-off counts as made once the command has not failed for a moment.
const openChatSettleMs = 2000;

interface AskResult {
    json: string | null;
    error: string | null;
}

/** Answers the LSP's requests for the models VS Code offers, and for an answer from one of them. */
export function registerLanguageModels(client: LanguageClient): void {
    client.onRequest(modelsRequestMethod, async (): Promise<{ models: ModelInfo[] }> => ({
        models: await listModelsAsync()
    }));
    client.onRequest(askRequestMethod, (params: AskParams, token: vscode.CancellationToken): Promise<AskResult> =>
        askAsync(params, token)
    );
    client.onRequest(turnRequestMethod, (params: TurnParams, token: vscode.CancellationToken): Promise<TurnResult> =>
        turnAsync(params, token)
    );
    client.onRequest(openChatRequestMethod, (params: OpenChatParams): Promise<{ error: string | null }> =>
        openAgentChatAsync(params.prompt)
    );
}

// "Fix with Copilot": the fix prompt goes to Copilot Chat in agent mode, which makes the change with
// its own tools and shows each edit to keep or undo. The extension's file watcher re-parses after.
async function openAgentChatAsync(prompt: string): Promise<{ error: string | null }> {
    const opened = Promise.resolve(
        vscode.commands.executeCommand("workbench.action.chat.open", { query: prompt, mode: "agent", isPartialQuery: false })
    ).then(
        () => null,
        (error: unknown) => {
            logger.error("Could not open Copilot Chat", error);
            return `Could not open Copilot Chat in agent mode: ${error instanceof Error ? error.message : String(error)}`;
        }
    );
    const settled = new Promise<null>(resolve => setTimeout(() => resolve(null), openChatSettleMs));
    return { error: await Promise.race([opened, settled]) };
}

async function listModelsAsync(): Promise<ModelInfo[]> {
    try {
        const models = await vscode.lm.selectChatModels();
        return models.map(m => ({
            id: m.id,
            name: m.name,
            vendor: m.vendor,
            family: m.family,
            maxInputTokens: m.maxInputTokens
        }));
    } catch (error) {
        logger.error("Could not list VS Code's language models", error);
        return [];
    }
}

async function askAsync(params: AskParams, requestToken: vscode.CancellationToken): Promise<AskResult> {
    const model = await selectModelAsync(params.modelId);
    if (!model)
        return failure(
            "VS Code offers no Copilot model. Install GitHub Copilot Chat and sign in, or pick another AI in Settings › AI."
        );

    let schema: object;
    try {
        schema = JSON.parse(params.jsonSchema) as object;
    } catch {
        return failure("The answer's JSON schema could not be read.");
    }

    const messages = [
        vscode.LanguageModelChatMessage.User(
            params.prompt +
                `\n\nGive your answer by calling the \`${answerToolName}\` tool once, with the answer as its input.`
        )
    ];
    const options: vscode.LanguageModelChatRequestOptions = {
        justification: "Dependinator asks the model about the dependencies in your code, when you ask for AI help.",
        tools: [{ name: answerToolName, description: "Gives the answer, in the required format.", inputSchema: schema }],
        toolMode: vscode.LanguageModelChatToolMode.Required
    };

    const cts = new vscode.CancellationTokenSource();
    const timer = setTimeout(() => cts.cancel(), askTimeoutMs);
    // The LSP gave up (its own time limit, or it stopped): no point in an answer nobody reads.
    const cancelled = requestToken.onCancellationRequested(() => cts.cancel());
    try {
        logger.log(`Asking ${model.vendor} model ${model.name} (${model.id})`);
        const response = await model.sendRequest(messages, options, cts.token);
        let text = "";
        for await (const part of response.stream) {
            if (part instanceof vscode.LanguageModelToolCallPart && part.name === answerToolName)
                return { json: JSON.stringify(part.input), error: null };
            if (part instanceof vscode.LanguageModelTextPart)
                text += part.value;
        }
        // A model that answered in text instead: its JSON, where there is some.
        const json = jsonIn(text);
        return json !== null ? { json, error: null } : failure(`${model.name} did not give an answer in the required format.`);
    } catch (error) {
        if (cts.token.isCancellationRequested)
            return failure(`${model.name} did not answer in time.`);
        return failure(errorText(error, model));
    } finally {
        clearTimeout(timer);
        cancelled.dispose();
        cts.dispose();
    }
}

// One turn: the model answers with text and tool calls, the answer tool's call among them when it
// is done. All tools stay declared on the last turn too (the history has calls of them, which some
// backends check, and some models take a required tool only when it is the only one); the LSP's
// prompt tells the model to answer now.
async function turnAsync(params: TurnParams, requestToken: vscode.CancellationToken): Promise<TurnResult> {
    const model = await selectModelAsync(params.modelId);
    if (!model)
        return {
            parts: null,
            error: "VS Code offers no Copilot model. Install GitHub Copilot Chat and sign in, or pick another AI in Settings › AI."
        };

    let answerTool: vscode.LanguageModelChatTool;
    let modelTools: vscode.LanguageModelChatTool[];
    let messages: vscode.LanguageModelChatMessage[];
    try {
        answerTool = {
            name: answerToolName,
            description: "Gives the final answer, in the required format. Call it once, when you are done looking things up.",
            inputSchema: JSON.parse(params.answerSchema) as object
        };
        modelTools = params.tools.map(t => ({
            name: t.name,
            description: t.description,
            inputSchema: JSON.parse(t.inputSchema) as object
        }));
        messages = params.messages.map(toChatMessage);
    } catch (error) {
        return { parts: null, error: `The request could not be read: ${error instanceof Error ? error.message : String(error)}` };
    }

    const options: vscode.LanguageModelChatRequestOptions = {
        justification: "Dependinator asks the model about the dependencies in your code, when you ask for AI help.",
        tools: [...modelTools, answerTool],
        toolMode: vscode.LanguageModelChatToolMode.Auto
    };

    const cts = new vscode.CancellationTokenSource();
    const timer = setTimeout(() => cts.cancel(), askTimeoutMs);
    const cancelled = requestToken.onCancellationRequested(() => cts.cancel());
    try {
        const response = await model.sendRequest(messages, options, cts.token);
        const parts: TurnPart[] = [];
        let text = "";
        for await (const part of response.stream) {
            if (part instanceof vscode.LanguageModelTextPart)
                text += part.value;
            else if (part instanceof vscode.LanguageModelToolCallPart)
                parts.push({ kind: "toolCall", callId: part.callId, name: part.name, input: JSON.stringify(part.input) });
        }
        if (text !== "")
            parts.unshift({ kind: "text", text });
        logger.log(`${model.name} answered a turn: ${parts.filter(p => p.kind === "toolCall").map(p => p.name).join(", ") || "text"}`);
        return { parts, error: null };
    } catch (error) {
        if (cts.token.isCancellationRequested)
            return { parts: null, error: `${model.name} did not answer in time.` };
        return { parts: null, error: errorText(error, model) };
    } finally {
        clearTimeout(timer);
        cancelled.dispose();
        cts.dispose();
    }
}

function toChatMessage(message: TurnMessage): vscode.LanguageModelChatMessage {
    if (message.role === "assistant") {
        const parts: (vscode.LanguageModelTextPart | vscode.LanguageModelToolCallPart)[] = message.parts.map(p =>
            p.kind === "toolCall"
                ? new vscode.LanguageModelToolCallPart(p.callId ?? "", p.name ?? "", JSON.parse(p.input ?? "{}") as object)
                : new vscode.LanguageModelTextPart(p.text ?? "")
        );
        return vscode.LanguageModelChatMessage.Assistant(parts);
    }
    const parts: (vscode.LanguageModelTextPart | vscode.LanguageModelToolResultPart)[] = message.parts.map(p =>
        p.kind === "toolResult"
            ? new vscode.LanguageModelToolResultPart(p.callId ?? "", [new vscode.LanguageModelTextPart(p.text ?? "")])
            : new vscode.LanguageModelTextPart(p.text ?? "")
    );
    return vscode.LanguageModelChatMessage.User(parts);
}

// The chosen model, or else the first of Copilot's. Never just any model: other extensions list
// placeholders too (Foundry Toolkit's "Select a Foundry project ..."), which cannot answer.
async function selectModelAsync(modelId: string | null): Promise<vscode.LanguageModelChat | undefined> {
    if (modelId) {
        const [chosen] = await vscode.lm.selectChatModels({ id: modelId });
        if (chosen)
            return chosen;
    }
    const [copilot] = await vscode.lm.selectChatModels({ vendor: "copilot" });
    return copilot;
}

function errorText(error: unknown, model: vscode.LanguageModelChat): string {
    if (error instanceof vscode.LanguageModelError) {
        logger.error(`Language model error ${error.code}`, error);
        switch (error.code) {
            case vscode.LanguageModelError.NoPermissions.name:
                return `Dependinator may not use ${model.name}: VS Code's permission was not given. Ask again to allow it.`;
            case vscode.LanguageModelError.Blocked.name:
                return `${model.name} is not available now, perhaps a quota limit was reached: ${error.message}`;
            case vscode.LanguageModelError.NotFound.name:
                return `${model.name} is no longer available. Pick another model in Settings › AI.`;
        }
        return `${model.name} could not answer: ${error.message}`;
    }
    logger.error("Language model request failed", error);
    return `${model.name} could not answer: ${error instanceof Error ? error.message : String(error)}`;
}

// The JSON object in a text answer: all of it, or the outermost braces (inside a code fence, say).
function jsonIn(text: string): string | null {
    const start = text.indexOf("{");
    const end = text.lastIndexOf("}");
    if (start < 0 || end <= start)
        return null;
    const candidate = text.slice(start, end + 1);
    try {
        JSON.parse(candidate);
        return candidate;
    } catch {
        return null;
    }
}

function failure(error: string): AskResult {
    return { json: null, error };
}

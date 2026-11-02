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

interface AskResult {
    json: string | null;
    error: string | null;
}

/** Answers the LSP's requests for the models VS Code offers, and for an answer from one of them. */
export function registerLanguageModels(client: LanguageClient): void {
    client.onRequest(modelsRequestMethod, async (): Promise<{ models: ModelInfo[] }> => ({
        models: await listModelsAsync()
    }));
    client.onRequest(askRequestMethod, (params: AskParams): Promise<AskResult> => askAsync(params));
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

async function askAsync(params: AskParams): Promise<AskResult> {
    const model = await selectModelAsync(params.modelId);
    if (!model)
        return failure(
            "VS Code offers no language model. Install GitHub Copilot and sign in, or pick another AI in Settings › AI."
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
        cts.dispose();
    }
}

// The chosen model, or else the first of Copilot's, or else any.
async function selectModelAsync(modelId: string | null): Promise<vscode.LanguageModelChat | undefined> {
    if (modelId) {
        const [chosen] = await vscode.lm.selectChatModels({ id: modelId });
        if (chosen)
            return chosen;
    }
    const [copilot] = await vscode.lm.selectChatModels({ vendor: "copilot" });
    if (copilot)
        return copilot;
    const [any] = await vscode.lm.selectChatModels();
    return any;
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

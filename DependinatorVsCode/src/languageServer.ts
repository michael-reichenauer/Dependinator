import * as vscode from "vscode";
import type { LanguageClient, LanguageClientOptions, ServerOptions } from "vscode-languageclient/node";

export async function startLanguageServer(
    context: vscode.ExtensionContext
): Promise<LanguageClient | undefined> {
    console.log("Starting Language Server in Extension");
    // Prefer a prebuilt DLL; fallback to `dotnet run` without build/restore noise.
    const workspaceFolder = vscode.workspace.workspaceFolders?.[0]?.uri;
    const workspaceProject = workspaceFolder
        ? vscode.Uri.joinPath(
            workspaceFolder,
            "DependinatorLanguageServer",
            "DependinatorLanguageServer.csproj"
        )
        : undefined;
    const extensionProject = vscode.Uri.joinPath(
        context.extensionUri,
        "..",
        "DependinatorLanguageServer",
        "DependinatorLanguageServer.csproj"
    );
    const serverExeName = process.platform === "win32" ? "DependinatorLanguageServer.exe" : "DependinatorLanguageServer";
    const runtimeIdentifier = getRuntimeIdentifier();
    const serverExeCandidates: vscode.Uri[] = [
        runtimeIdentifier
            ? vscode.Uri.joinPath(
                context.extensionUri,
                "server",
                runtimeIdentifier,
                serverExeName
            )
            : undefined,
        vscode.Uri.joinPath(
            context.extensionUri,
            "server",
            serverExeName
        )
    ].filter((candidate): candidate is vscode.Uri => !!candidate);

    const serverDllCandidates: vscode.Uri[] = [
        vscode.Uri.joinPath(
            context.extensionUri,
            "server",
            "DependinatorLanguageServer.dll"
        ),
        vscode.Uri.joinPath(
            context.extensionUri,
            "..",
            "DependinatorLanguageServer",
            "bin",
            "Debug",
            "net10.0",
            "DependinatorLanguageServer.dll"
        ),
        vscode.Uri.joinPath(
            context.extensionUri,
            "..",
            "DependinatorLanguageServer",
            "bin",
            "Release",
            "net10.0",
            "DependinatorLanguageServer.dll"
        )
    ].filter((candidate): candidate is vscode.Uri => !!candidate);

    let serverCommand = "dotnet";
    let serverArgs: string[] | undefined;
    const serverExe = await firstExisting(serverExeCandidates);
    if (serverExe) {
        serverCommand = serverExe.fsPath;
        serverArgs = [];
    } else {
        const serverDll = await firstExisting(serverDllCandidates);
        if (serverDll) {
            serverArgs = [serverDll.fsPath];
        } else {
            const projectCandidates = [workspaceProject, extensionProject].filter(
                (candidate): candidate is vscode.Uri => !!candidate
            );
            const project = await firstExisting(projectCandidates);
            if (project) {
                serverArgs = [
                    "run",
                    "--project",
                    project.fsPath,
                    "--no-build",
                    "--no-restore",
                    "--nologo"
                ];
            } else {
                console.warn("Dependinator language server project not found.");
                return undefined;
            }
        }
    }

    const { LanguageClient, TransportKind } = await import("vscode-languageclient/node");

    // Keep stdout clean for JSON-RPC by suppressing CLI banners.
    const environment = {
        ...process.env,
        DOTNET_NOLOGO: "1",
        NUGET_XMLDOC_MODE: "skip",
        DOTNET_CLI_TELEMETRY_OPTOUT: "1"
    };
    const executableOptions = workspaceFolder ? { cwd: workspaceFolder.fsPath, env: environment } : { env: environment };
    const serverOptions: ServerOptions = {
        run: {
            command: serverCommand,
            args: serverArgs,
            transport: TransportKind.stdio,
            options: executableOptions
        },
        debug: {
            command: serverCommand,
            args: serverArgs,
            transport: TransportKind.stdio,
            options: executableOptions
        }
    };

    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: "file", language: "csharp" }]
    };

    const client = new LanguageClient(
        "dependinatorLanguageServer",
        "Dependinator Language Server",
        serverOptions,
        clientOptions
    );

    console.log("Starting lsp client ...");
    await client.start();
    console.log("Started lsp client");
    context.subscriptions.push(client);
    return client;
}

export function registerLanguageClientLogging(client: LanguageClient): void {
    client.onNotification("vscode/log", params => {
        const type = typeof params?.Type === "string" ? params.Type.toLowerCase() : "";
        const message = params?.Message ?? params?.message ?? "";

        switch (type) {
            case "warning":
            case "warn":
                console.warn(message);
                break;
            case "error":
            case "err":
                console.error(message);
                break;
            case "log":
            case "info":
            default:
                console.log(message);
                break;
        }
    });
}

export function registerUiMessageForwarding(
    client: LanguageClient,
    getWebview: () => vscode.Webview | undefined
): void {
    client.onNotification("ui/message", params => {
        // console.log("ui/message:", params);
        getWebview()?.postMessage({
            type: "ui/message",
            message: params?.message
        });
    });
    client.onNotification("ui/lspReady", params => {
        console.log("ui/lspReady:", params);
        getWebview()?.postMessage({
            type: "ui/lspReady",
            message: params?.message
        });
    });
}

async function fileExists(uri: vscode.Uri): Promise<boolean> {
    try {
        await vscode.workspace.fs.stat(uri);
        return true;
    } catch {
        return false;
    }
}

async function firstExisting(uris: vscode.Uri[]): Promise<vscode.Uri | undefined> {
    console.log("Lsp candidate paths: ", uris);
    for (const uri of uris) {
        if (await fileExists(uri)) {
            console.log("Lsp Exist path: ", uri);
            return uri;
        }
    }
    return undefined;
}

function getRuntimeIdentifier(): string | undefined {
    const platform = process.platform;
    const arch = process.arch;
    const archSuffix = arch === "x64" ? "x64" : arch === "arm64" ? "arm64" : undefined;
    if (!archSuffix)
        return undefined;

    switch (platform) {
        case "win32":
            return `win-${archSuffix}`;
        case "linux":
            return `linux-${archSuffix}`;
        case "darwin":
            return `osx-${archSuffix}`;
        default:
            return undefined;
    }
}

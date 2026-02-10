import * as vscode from "vscode";
import type { LanguageClient, LanguageClientOptions, ServerOptions } from "vscode-languageclient/node";

export async function startLanguageServer(
    context: vscode.ExtensionContext
): Promise<LanguageClient | undefined> {
    console.log("DEP: Starting Language Server in Extension");
    // Prefer a prebuilt DLL; fallback to `dotnet run` without build/restore noise.
    const workspaceFolder = vscode.workspace.workspaceFolders?.[0]?.uri;
    const isRemoteWorkspace = !!workspaceFolder && workspaceFolder.scheme !== "file";
    const isRemoteExtensionHost = context.extensionUri.scheme !== "file";
    // Avoid launching a server that can't access the workspace filesystem.
    if (isRemoteWorkspace && !isRemoteExtensionHost) {
        const action = await vscode.window.showWarningMessage(
            "Dependinator language server needs to run in the dev container. Install the extension in the dev container to enable it.",
            "Install in Dev Container"
        );
        if (action === "Install in Dev Container") {
            await vscode.commands.executeCommand("dependinator.installInDevContainer");
        }
        return undefined;
    }
    const workspaceProject = workspaceFolder
        ? vscode.Uri.joinPath(
            workspaceFolder,
            "DependinatorLanguageServer",
            "DependinatorLanguageServer.csproj"
        )
        : undefined;
    console.log("DEP: workspaceProject", workspaceProject);

    const extensionProject = vscode.Uri.joinPath(
        context.extensionUri,
        "..",
        "DependinatorLanguageServer",
        "DependinatorLanguageServer.csproj"
    );
    console.log("DEP: extensionProject", extensionProject);

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
    console.log("DEP: serverExeCandidates", serverExeCandidates);

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
    console.log("DEP: serverDllCandidates", serverDllCandidates);

    let serverCommand = "dotnet";
    let serverArgs: string[] | undefined;
    const serverExe = await firstExisting(serverExeCandidates);
    if (serverExe) {
        serverCommand = serverExe.fsPath;
        serverArgs = [];
        console.log("DEP: Using server exe:", serverExe.fsPath);
    } else {
        const serverDll = await firstExisting(serverDllCandidates);
        if (serverDll) {
            serverArgs = [serverDll.fsPath];
            console.log("DEP: Using server dll:", serverDll.fsPath);
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
                console.log("DEP: Using server project:", project.fsPath);
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
    const executableOptions = workspaceFolder
        ? { cwd: workspaceFolder.fsPath, env: environment }
        : { env: environment };
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

    console.log("DEP: Starting lsp client ...", serverOptions);
    await client.start();
    console.log("DEP: Started lsp client", serverOptions);
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
    getWebview: () => vscode.Webview | undefined,
    onLspReady?: (params: unknown) => void
): void {
    client.onNotification("ui/message", params => {
        // console.log("DEP: ui/message:", params);
        getWebview()?.postMessage({
            type: "ui/message",
            message: params?.message
        });
    });
    client.onNotification("ui/lspReady", params => {
        console.log("DEP: ui/lspReady:", params);
        onLspReady?.(params);
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
    console.log("DEP: Lsp candidate paths: ", uris);
    for (const uri of uris) {
        if (await fileExists(uri)) {
            console.log("DEP: Lsp Exist path: ", uri);
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

"""Generates the Dependinator architecture diagrams (docs/ARCHITECTURE.md) as self-contained SVGs:
hand-placed nodes with icons from Dependinator's own icon library, cards, groups and labeled edges.
The same approach as the webshop example's diagrams (examples/bicep/architecture/generate.py):
strokes and text use currentColor and fills use CSS variables with light fallbacks, so the files
render standalone (GitHub, image viewers) and follow the theme when inlined in a page.

Run from anywhere: python3 docs/architecture/generate.py"""
import re, html, pathlib

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
ICON_DIR = REPO / "src/Dependinator.UI/Diagrams/Icons/Library"
ACCENT = "var(--accent, #2563eb)"     # main request or data path (themeable when inlined)
ASYNC = "var(--accent2, #b45309)"     # messages, background and optional flows
BG = "var(--bg, #ffffff)"

def icon_symbol(name):
    for group in ("Azure", "General", "Default"):
        p = ICON_DIR / group / f"{name}.svg"
        if p.exists():
            s = p.read_text()
            inner = re.sub(r"^<svg[^>]*>", "", s.strip())
            inner = re.sub(r"</svg>\s*$", "", inner)
            vb = re.search(r'viewBox="([^"]+)"', s).group(1)
            return f'<symbol id="ic-{name}" viewBox="{vb}">{inner}</symbol>'
    raise KeyError(name)

def esc(text):
    return html.escape(text)

class Diagram:
    def __init__(self, w, h, title):
        self.w, self.h, self.title = w, h, title
        self.icons = set()
        self.body = []
        self.titles = []
        self.nodes = {}   # key -> (center x, center y, half width, half height) used to stop edges

    def group(self, x, y, w, h, title, sub=None, dashed=False, tint=None):
        dash = ' stroke-dasharray="6 4"' if dashed else ""
        fill = f'fill="{tint}" fill-opacity="0.08"' if tint else 'fill="currentColor" fill-opacity="0.035"'
        self.body.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="10" {fill} stroke="currentColor" stroke-opacity="0.35"{dash}/>')
        # Group titles are drawn later, above the edges (see render), with a halo so a line that
        # passes under one stays readable.
        halo = f'style="paint-order:stroke;stroke:{BG};stroke-width:4px;stroke-linejoin:round"'
        self.titles.append(f'<text x="{x+12}" y="{y+20}" font-size="13" font-weight="600" fill="currentColor" {halo}>{esc(title)}</text>')
        if sub:
            self.titles.append(f'<text x="{x+12}" y="{y+36}" font-size="11" fill="currentColor" fill-opacity="0.7" {halo}>{esc(sub)}</text>')

    def node(self, key, x, y, icon, label, sub=None, size=44, dashed=False):
        """Icon centered at (x, y) with the label below."""
        self.icons.add(icon)
        half = size / 2
        self.body.append(f'<use href="#ic-{icon}" x="{x-half}" y="{y-half}" width="{size}" height="{size}"/>')
        if dashed:
            self.body.append(f'<rect x="{x-half-6}" y="{y-half-6}" width="{size+12}" height="{size+12}" rx="8" fill="none" stroke="currentColor" stroke-opacity="0.5" stroke-dasharray="4 3"/>')
        ty = y + half + 14
        for i, line in enumerate(label.split("\n")):
            self.body.append(f'<text x="{x}" y="{ty + i*13}" font-size="12" font-weight="600" text-anchor="middle" fill="currentColor">{esc(line)}</text>')
        n = label.count("\n") + 1
        if sub:
            for i, line in enumerate(sub.split("\n")):
                self.body.append(f'<text x="{x}" y="{ty + n*13 + i*12}" font-size="10.5" text-anchor="middle" fill="currentColor" fill-opacity="0.72">{esc(line)}</text>')
        self.nodes[key] = (x, y, half + 6, half + 6)

    def box(self, key, x, y, w, h, title, lines=(), icon=None, dashed=False, tint=None, size=11):
        """A card: title (with an optional icon) and text lines. Registers the card and its side
        midpoints as ports key.t, key.b, key.l and key.r for edges."""
        dash = ' stroke-dasharray="5 4"' if dashed else ""
        fill = f'fill="{tint}" fill-opacity="0.10"' if tint else f'fill="{BG}"'
        self.body.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="8" {fill} stroke="currentColor" stroke-opacity="0.55"{dash}/>')
        tx = x + 12
        if icon:
            self.icons.add(icon)
            self.body.append(f'<use href="#ic-{icon}" x="{x+10}" y="{y+9}" width="24" height="24"/>')
            tx = x + 42
        self.body.append(f'<text x="{tx}" y="{y+26}" font-size="12.5" font-weight="600" fill="currentColor">{esc(title)}</text>')
        for i, line in enumerate(lines):
            bold = line.startswith("**")
            text = line.strip("*")
            weight = ' font-weight="600"' if bold else ""
            self.body.append(f'<text x="{x+12}" y="{y+48 + i*(size+4)}" font-size="{size}"{weight} fill="currentColor" fill-opacity="{0.95 if bold else 0.78}">{esc(text)}</text>')
        cx, cy = x + w / 2, y + h / 2
        self.nodes[key] = (cx, cy, w / 2, h / 2)
        for port, (px, py) in {"t": (cx, y), "b": (cx, y + h), "l": (x, cy), "r": (x + w, cy)}.items():
            self.nodes[f"{key}.{port}"] = (px, py, 0, 0)

    def port(self, key, x, y):
        self.nodes[key] = (x, y, 0, 0)

    def person(self, key, x, y, label, sub=None):
        self.body.append(f'<circle cx="{x}" cy="{y-10}" r="9" fill="none" stroke="currentColor" stroke-width="1.6"/>')
        self.body.append(f'<path d="M {x-16} {y+16} a 16 14 0 0 1 32 0" fill="none" stroke="currentColor" stroke-width="1.6"/>')
        ty = y + 30
        self.body.append(f'<text x="{x}" y="{ty}" font-size="12" font-weight="600" text-anchor="middle" fill="currentColor">{esc(label)}</text>')
        if sub:
            for i, line in enumerate(sub.split("\n")):
                self.body.append(f'<text x="{x}" y="{ty+12+i*12}" font-size="10.5" text-anchor="middle" fill="currentColor" fill-opacity="0.72">{esc(line)}</text>')
        self.nodes[key] = (x, y, 24, 26)

    def note(self, x, y, lines, size=11, anchor="start"):
        for i, line in enumerate(lines):
            bold = line.startswith("**")
            text = line.strip("*")
            weight = ' font-weight="600"' if bold else ""
            self.body.append(f'<text x="{x}" y="{y + i*15}" font-size="{size}"{weight} text-anchor="{anchor}" fill="currentColor" fill-opacity="{1 if bold else 0.8}">{esc(text)}</text>')

    def divider(self, x, y1, y2, label_lines):
        """A dashed vertical boundary (e.g. a process boundary) with a label at its top."""
        self.body.append(f'<path d="M {x} {y1} L {x} {y2}" stroke="currentColor" stroke-opacity="0.55" stroke-width="1.4" stroke-dasharray="3 4"/>')
        for i, line in enumerate(label_lines):
            self.body.append(f'<text x="{x}" y="{y1 - 8 - (len(label_lines)-1-i)*12}" font-size="10.5" text-anchor="middle" fill="currentColor" fill-opacity="0.8">{esc(line)}</text>')

    def edge(self, a, b, label=None, color=None, dashed=False, via=None, label_at=0.5, label_dy=-6, label_dx=0, anchor="middle", both=False):
        """Edge from node a to node b through optional waypoints; the ends stop at the nodes' edges."""
        ax, ay, aw, ah = self.nodes[a]
        bx, by, bw, bh = self.nodes[b]
        pts = [(ax, ay)] + list(via or []) + [(bx, by)]
        def trim(p, q, hw, hh):
            dx, dy = q[0]-p[0], q[1]-p[1]
            tx = hw / abs(dx) if dx else float("inf")
            ty = hh / abs(dy) if dy else float("inf")
            t = min(tx, ty, 1.0)
            return (p[0] + dx*t, p[1] + dy*t)
        pts[0] = trim(pts[0], pts[1], aw, ah)
        pts[-1] = trim(pts[-1], pts[-2], bw, bh)
        d = "M " + " L ".join(f"{x:.1f} {y:.1f}" for x, y in pts)
        stroke = color or "currentColor"
        dash = ' stroke-dasharray="6 4"' if dashed else ""
        marker = "arrow-accent" if color == ACCENT else "arrow-async" if color == ASYNC else "arrow"
        start = f' marker-start="url(#{marker})"' if both else ""
        self.body.append(f'<path d="{d}" fill="none" stroke="{stroke}" stroke-width="1.5"{dash}{start} marker-end="url(#{marker})"/>')
        if label:
            i = min(int(label_at * (len(pts)-1)), len(pts)-2)
            t = label_at * (len(pts)-1) - i
            mx = pts[i][0] + (pts[i+1][0]-pts[i][0]) * t
            my = pts[i][1] + (pts[i+1][1]-pts[i][1]) * t
            fill = color or "currentColor"
            for j, line in enumerate(label.split("\n")):
                self.body.append(f'<text x="{mx + label_dx:.1f}" y="{my + label_dy + j*12:.1f}" font-size="10.5" text-anchor="{anchor}" fill="{fill}" style="paint-order:stroke;stroke:{BG};stroke-width:3px;stroke-linejoin:round">{esc(line)}</text>')

    def render(self, aria):
        defs = "".join(icon_symbol(i) for i in sorted(self.icons))
        markers = "".join(
            f'<marker id="{mid}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse"><path d="M 0 0 L 10 5 L 0 10 z" fill="{c}"/></marker>'
            for mid, c in (("arrow", "currentColor"), ("arrow-accent", ACCENT), ("arrow-async", ASYNC)))
        return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {self.w} {self.h}" role="img" aria-label="{esc(aria)}" '
                f'font-family="Segoe UI, Inter, system-ui, sans-serif" style="max-width:100%;height:auto;color:var(--fg,#1f2937)">'
                f'<title>{esc(self.title)}</title><defs>{markers}{defs}</defs>'
                f'<rect width="{self.w}" height="{self.h}" fill="{BG}"/>' + "".join(self.body) + "".join(self.titles) + "</svg>")

def write(name, d, aria):
    (HERE / name).write_text(d.render(aria))
    print("wrote", name, len(d.render(aria)) // 1024, "KB")


# ================================================================================================
# The diagrams
# ================================================================================================

# ------------------------------------------------------------------------------------------
# 1. Context view: who uses Dependinator, and what it talks to
# ------------------------------------------------------------------------------------------
d = Diagram(1400, 790, "Dependinator context view")
d.group(30, 70, 200, 630, "People")
d.group(280, 70, 660, 630, "Dependinator", "one Blazor UI, three hosts, one small cloud API")
d.group(990, 70, 380, 630, "External", "not run by Dependinator")

d.person("developer", 130, 182, "Developer", "VS Code, a C# solution\nor a Bicep deployment")
d.person("visitor", 130, 382, "Developer, anywhere", "a browser on\nany device")
d.person("contributor", 130, 582, "Contributor", "works on Dependinator\nitself")

d.box("vscode", 320, 120, 300, 125, "VS Code extension", [
    "the product, on the VS Code Marketplace",
    "parses the open workspace and draws it",
    "jumps between the diagram and the code",
    "webview UI + bundled .NET language server"], icon="View", tint=ACCENT)
d.box("web", 320, 320, 300, 125, "Web app · dependinator.com", [
    "view synced models from anywhere",
    "and make small adjustments; share links",
    "demo models to get started",
    "Azure Static Web Apps"], icon="Static-Web-Apps")
d.box("dev", 320, 520, 300, 125, "Dev host · localhost:5000", [
    "Blazor Server, for development",
    "parses this repository with Roslyn",
    "the app the e2e tests drive",
    "./scripts/watch, ./scripts/e2e"], icon="Testing")
d.box("api", 690, 320, 220, 125, "Cloud sync API", [
    "Azure Functions (src/Api)",
    "+ Blob Storage",
    "one folder per user",
    "at most 10 MB per user"], icon="Azure-Functions")

d.node("src", 1180, 182, "Solution", "Workspace source", ".sln + C# projects,\n.bicep entry files + local modules")
d.node("clerk", 1180, 382, "Authentication", "Clerk", "hosted sign-in, issues the\nJWTs the API checks")
d.node("claude", 1180, 582, "Api", "Anthropic Claude API", "optional, with the user's own key")

d.edge("developer", "vscode", "Dependinator: Open", ACCENT, label_dy=-8)
d.edge("visitor", "web", "HTTPS", ACCENT, label_dy=-8)
d.edge("contributor", "dev", "browser", ACCENT, label_dy=-8)
d.edge("vscode.r", "src", "reads and parses the workspace", ACCENT, label_at=0.45, label_dy=-8)
d.port("vscode.sync", 560, 245)
d.edge("vscode.sync", "api.t", "optional sync", ASYNC, dashed=True, via=[(560, 280), (800, 280)], label_at=0.62, label_dy=-6)
d.edge("web.r", "api.l", "sync", ASYNC, dashed=True, label_dy=-8)
d.edge("api.r", "clerk", "validates sign-in tokens", label_at=0.68, label_dy=-8)
d.port("vscode.ai", 620, 225); d.port("claude.in", 1152, 565)
d.edge("vscode.ai", "claude.in", "\"Ask Claude\" about a cycle", via=[(965, 225), (965, 565)], label_at=0.2, label_dy=-6)
d.port("dev.ai", 620, 600); d.port("claude.in2", 1152, 600)
d.edge("dev.ai", "claude.in2", "\"Ask Claude\" (dev host)", label_at=0.45, label_dy=-6)
d.note(40, 735, ["Solid blue: how people reach the product and how it reads code. Dashed brown: the optional cloud sync.",
                 "Parsing runs locally, in VS Code or the dev host. Cloud sync stores the drawn model (nodes, links, layout, notes), never source files;",
                 "asking Claude about a cycle sends a prompt with the code excerpts involved, with the user's own API key."], size=11)
write("1-context.svg", d, "Context view of Dependinator: developers use the VS Code extension, which parses their workspace; the same users view and adjust their synced models on dependinator.com from any device; contributors use the Blazor Server dev host. The VS Code extension and the web app can sync models through a small Azure Functions API with Blob Storage that validates Clerk sign-in tokens; the VS Code extension and the dev host call the Claude API, optionally, with the user's own key.")

# ------------------------------------------------------------------------------------------
# 2. Hosts view: where the code runs
# ------------------------------------------------------------------------------------------
d = Diagram(1500, 700, "Dependinator hosts view")
d.group(20, 60, 520, 620, "VS Code (the product)", "src/DependinatorVsCode · one .vsix per platform on the Marketplace")
d.box("ext", 50, 110, 460, 125, "Extension host · TypeScript", [
    "extension.ts: commands, status bar, follow the active editor,",
    "    auto refresh on file changes, open a node's source",
    "languageServer.ts: starts the bundled server (server/<rid>/)",
    "cloudSyncAuth.ts, claudeApiKey.ts: sign-in, secret storage"], icon="Utility")
d.box("webview", 50, 310, 220, 140, "Webview", [
    "**Blazor WebAssembly",
    "Dependinator.Wasm, .UI, .Core",
    "(parsing calls are RPC proxies)",
    "the same UI as dependinator.com"], icon="View", tint=ACCENT)
d.box("lsp", 290, 310, 220, 140, "Language server", [
    "**.NET, self-contained",
    "Dependinator.Lsp + Core,",
    "Roslyn, Bicep, Claude",
    "OmniSharp LSP over stdio"], icon="Parsing")
d.node("idb1", 160, 565, "Database", "IndexedDB", "model cache,\nin the webview")
d.node("ws", 400, 565, "Solution", "Workspace folders", ".sln, .bicep, sources;\nMSBuild needs a .NET SDK")
d.port("ext.b1", 160, 235); d.port("ext.b2", 400, 235)
d.edge("ext.b1", "webview.t", "postMessage", ASYNC, dashed=True, both=True, label_dx=6, anchor="start", label_dy=4)
d.edge("ext.b2", "lsp.t", "LSP, stdio", ASYNC, dashed=True, both=True, label_dx=6, anchor="start", label_dy=4)
d.edge("lsp.b", "ws", "MSBuild, Roslyn,\nBicep compiler", ACCENT, label_dx=6, anchor="start", label_dy=-4)
d.edge("webview.b", "idb1", None)

d.group(570, 60, 480, 620, "Browser · dependinator.com", "Azure Static Web Apps: releases = production, previews = pre-release")
d.box("browser", 600, 110, 420, 140, "Browser · Blazor WebAssembly", [
    "view and adjust synced models from any device",
    "Dependinator.Wasm + UI + Core: the same UI as in VS Code",
    "no parser in the browser; demo models to get started are",
    "parsed at build time (./scripts/gen-demo); IndexedDB cache"], icon="View", tint=ACCENT)
d.node("swa", 690, 360, "Static-Web-Apps", "Static content", "the published Wasm app,\ndemo.model, demo-bicep.model")
d.node("func", 930, 360, "Azure-Functions", "src/Api", "Functions, isolated worker,\n/api/auth/me, /api/models")
d.node("clerk2", 690, 570, "Authentication", "Clerk", "sign-in (Clerk.js)")
d.node("blob", 930, 570, "Blob-Storage", "Blob Storage", "container dependinator-models,\none folder per user")
d.port("browser.b1", 690, 250); d.port("browser.b2", 930, 250); d.port("func.b1", 930, 420)
d.edge("browser.b1", "swa", "app + demo models", ACCENT, label_dx=6, anchor="start", label_dy=4)
d.edge("browser.b2", "func", "/api, Bearer JWT", ASYNC, dashed=True, label_dx=6, anchor="start", label_dy=4)
d.edge("func.b1", "blob", "read / write", label_dx=6, anchor="start", label_dy=4)
d.port("func.l1", 902, 375)
d.edge("func.l1", "clerk2", "signing keys\n(JWKS)", dashed=True, via=[(820, 375), (820, 570)], label_at=0.62, label_dx=-6, anchor="end")

d.group(1080, 60, 400, 620, "Development", "the dev container and ./scripts")
d.box("webhost", 1110, 110, 340, 140, "Blazor Server · localhost:5000", [
    "Dependinator.Web + UI + Core, with Roslyn,",
    "Bicep and Claude in the same process",
    "the UI runs on the server (SignalR circuit)",
    "parses this repository; the e2e test target"], icon="Testing", tint=ACCENT)
d.node("funclocal", 1200, 360, "Azure-Functions", "Functions host", "func start, src/Api")
d.node("azurite", 1370, 360, "Storage-Account", "Azurite", "local Blob emulator")
d.box("tools", 1110, 500, 340, 125, "Dev tools (outside the solution)", [
    "Dependinator.DemoGen: regenerates the demo models",
    "scripts/: build, watch, run, test, e2e, changelog",
    "dev container: .NET 11 preview SDK, Playwright",
    "browsers, Functions Core Tools, Azurite"], icon="Configuration")
d.port("webhost.b1", 1200, 250)
d.edge("webhost.b1", "funclocal", "sync tests (./scripts/e2e -s)", ASYNC, dashed=True, label_dx=6, anchor="start", label_dy=4)
d.edge("funclocal", "azurite", None)
write("2-hosts.svg", d, "Hosts view of Dependinator: in VS Code the extension host (TypeScript) opens a webview that runs the Blazor WebAssembly UI and starts a self-contained .NET language server that parses the workspace; dependinator.com serves the same WebAssembly UI from Azure Static Web Apps, with a Functions API over Blob Storage for cloud sync; for development a Blazor Server host runs UI and parsers in one process, with a local Functions host and Azurite.")

# ------------------------------------------------------------------------------------------
# 3. Code structure view: projects, layers and the rules that guard them
# ------------------------------------------------------------------------------------------
d = Diagram(1440, 900, "Dependinator code structure view")
d.group(30, 60, 1110, 150, "Hosts", "thin: wire up services and start")
d.box("h-web", 60, 100, 190, 90, "Dependinator.Web", ["Blazor Server dev host", "0.2k lines"])
d.box("h-wasm", 270, 100, 190, 90, "Dependinator.Wasm", ["WebAssembly app", "0.1k lines"])
d.box("h-lsp", 480, 100, 190, 90, "Dependinator.Lsp", ["language server", "0.5k lines"])
d.box("h-api", 690, 100, 190, 90, "Api", ["Azure Functions", "0.7k lines"])
d.box("h-ext", 900, 100, 220, 90, "DependinatorVsCode", ["TypeScript, npm, not in the .sln", "1.3k lines"], dashed=True)

d.group(30, 240, 1110, 170, "Dependinator.UI · 28.7k lines", "Blazor + MudBlazor, shared by every host")
d.box("u-app", 60, 285, 240, 110, "App/", ["app bar and menus, model list,", "dialogs, breadcrumb, progress,", "problem indicator, cloud sync button"])
d.box("u-diag", 320, 285, 300, 110, "Diagrams/", ["canvas, SVG rendering and tiles, pan/zoom", "and editing gestures, dependency explorer,", "cycles, rules, paths, search, export, icons"], tint=ACCENT)
d.box("u-model", 640, 285, 230, 110, "Modeling/", ["Model (nodes, links, lines),", "layout engine, undoable commands,", "persisted DTOs"], tint=ACCENT)
d.box("u-shared", 890, 285, 230, 110, "Shared/", ["IndexedDB, cloud sync, VS Code", "bridge, navigation, share links,", "events, themes, keyboard"])

d.group(30, 440, 1110, 140, "Parsers and integrations", "each plugs into Core")
d.box("p-roslyn", 60, 480, 190, 85, "Dependinator.Roslyn", ["C# solutions: MSBuildWorkspace", "1.8k lines"], icon="Parsing")
d.box("p-bicep", 270, 480, 190, 85, "Dependinator.Bicep", ["Bicep: Azure.Bicep.Core", "1.0k lines"], icon="Parsing")
d.box("p-claude", 480, 480, 190, 85, "Dependinator.Claude", ["Anthropic SDK: reviews, Ask", "0.9k lines"], icon="Api")
d.box("p-mcp", 690, 480, 190, 85, "Dependinator.Mcp", ["MCP server: model tools", "0.3k lines"], icon="Api")
d.box("p-refl", 900, 480, 220, 85, "Dependinator.Reflection", ["assemblies (Mono.Cecil), 2.5k", "inactive, no host uses it"], dashed=True)

d.group(30, 610, 1110, 150, "Dependinator.Core · 3.5k lines", "no UI framework, no parser library")
d.box("c-parse", 60, 655, 200, 90, "Parsing/", ["IParserService [Rpc],", "ISourceParser, Item types"], tint=ACCENT)
d.box("c-rpc", 280, 655, 200, 90, "Rpc/", ["JsonRpcService ([Rpc]", "interfaces, StreamJsonRpc)"])
d.box("c-sync", 500, 655, 180, 90, "CloudSync/", ["HTTP client for the", "sync API"])
d.box("c-shared", 700, 655, 200, 90, "Shared/", ["workspace files, model", "paths, demo models"])
d.box("c-utils", 920, 655, 200, 90, "Utils/", ["Result / Error unions,", "logging, timing"])

d.group(30, 790, 1110, 85, "Shared · 0.1k lines", "DTOs used by the client and the API")
d.box("s-dto", 600, 812, 520, 45, "CloudSyncContracts, BlobNameSanitizer")

d.port("web-down", 155, 190); d.port("app-top", 155, 285)
d.edge("web-down", "app-top", None)
d.port("wasm-down", 365, 190); d.port("diag-top", 365, 285)
d.edge("wasm-down", "diag-top", None)
d.port("lsp-down", 630, 190); d.port("claude-top", 630, 480)
d.edge("lsp-down", "claude-top", "Lsp: Core + parsers", label_at=0.81, label_dx=6, anchor="start", label_dy=4)
d.port("model-down", 850, 395); d.port("cshared-top", 890, 655)
d.edge("model-down", "cshared-top", "UI → Core", via=[(850, 422), (890, 422)], label_at=0.5, label_dx=6, anchor="start", label_dy=-6)
d.edge("h-api.b", "s-dto.r", "Api: Shared only", via=[(785, 225), (1160, 225), (1160, 835)], label_at=0.652, label_dx=-6, anchor="end", label_dy=4)
for x in (155, 365, 575, 785):
    d.port(f"p{x}a", x, 565); d.port(f"p{x}b", x, 655)
    d.edge(f"p{x}a", f"p{x}b", None)
d.note(161, 600, ["parsers → Core"], size=10.5)
d.port("refl-a", 1020, 565); d.port("refl-b", 1020, 655)
d.edge("refl-a", "refl-b", None, dashed=True)
d.port("core-a", 1060, 745); d.port("core-b", 1060, 812)
d.edge("core-a", "core-b", None)
d.note(1066, 784, ["Core → Shared"], size=10.5)

d.group(1180, 60, 240, 815, "Tests · 25k lines", "tests/, xUnit + Moq + Verify")
d.note(1194, 120, ["**Unit tests (dotnet test)",
                   "UI.Tests · 15.1k",
                   "Roslyn.Tests · 1.6k",
                   "Core.Tests · 1.4k",
                   "Api.Tests · 1.0k",
                   "Bicep.Tests · 0.7k",
                   "Reflection.Tests · 0.5k",
                   "Lsp.Tests · 0.4k",
                   "Claude.Tests · 0.2k",
                   "Mcp.Tests · 0.2k",
                   "",
                   "**UI tests (./scripts/e2e)",
                   "E2E.Tests · 4.4k",
                   "Playwright against the dev",
                   "host: Chromium, Firefox and",
                   "WebKit, plus cloud sync",
                   "against Azurite",
                   "",
                   "**Architecture.Tests",
                   "NetArchTest rules that fail",
                   "when a reference points",
                   "the wrong way:",
                   "· Core uses no UI, parser, host,",
                   "  MudBlazor or ASP.NET Core",
                   "· UI uses no host",
                   "· parsers use no UI or host",
                   "  and not each other",
                   "· only Dependinator.Claude",
                   "  touches the Anthropic SDK",
                   "· the MCP SDK stays out of",
                   "  Core and UI",
                   "· Shared uses nothing of ours"], size=11)
write("3-code-structure.svg", d, "Code structure view of Dependinator: thin hosts (Web, Wasm, Lsp, Api, and the TypeScript extension) on top of Dependinator.UI, which holds the App, Diagrams, Modeling and Shared areas; the Roslyn, Bicep, Claude and Mcp libraries plug into Dependinator.Core, which holds parsing contracts, RPC, cloud sync client, shared services and utilities; Shared holds the DTOs the API also uses. Architecture tests enforce that references only point down.")

# ------------------------------------------------------------------------------------------
# 4. Runtime view: from source code to a picture on the screen
# ------------------------------------------------------------------------------------------
d = Diagram(1500, 960, "Dependinator runtime view")
d.group(20, 60, 610, 380, "1 · Parse", "in the language server (VS Code) or the dev host; a demo file in the browser")
d.node("src", 90, 240, "Solution", "Source", ".sln and C# files,\n.bicep files")
d.node("roslyn", 300, 135, "Parsing", "Roslyn parser", "a compilation per project:\ntypes, members, references")
d.node("bicep", 300, 240, "Parsing", "Bicep parser", "the Bicep compiler's\nsemantic model and graph")
d.node("demo", 300, 345, "Files", "Demo model", "parsed at build time,\ngzipped JSON")
d.node("items", 520, 240, "Files", "Items", "a flat list of Node, Link,\nLineDescription, ParseProblem")
for p in ("roslyn", "bicep"):
    d.edge("src", p, None, ACCENT)
for p in ("roslyn", "bicep", "demo"):
    d.edge(p, "items", None, ACCENT)
d.note(420, 395, ["ParserService picks the first", "parser whose CanParse(path) matches."], size=10.5)

d.divider(655, 70, 440, [])

d.group(680, 60, 800, 380, "2 · Model", "Dependinator.UI, in the webview or the browser tab")
d.node("ms", 770, 240, "Utility", "ModelService", "opens the cached model first,\nadds items in batches of 100")
d.node("ss", 965, 240, "Namespace", "StructureService", "nodes by dotted name,\nparents, links, line funnels")
d.node("model", 1160, 240, "Assembly", "Model", "node tree, links, lines,\nlayout, notes, rules")
d.node("idb", 1390, 160, "Database", "IndexedDB", "a ModelDto per model path,\nsaved after edits (debounced)")
d.node("cloud", 1390, 350, "Blob-Storage", "Cloud sync", "optional (view 6)")
d.edge("items", "ms", "IParserService [Rpc]:\nJSON-RPC in VS Code,\na local call elsewhere", ACCENT, label_dy=-34)
d.edge("ms", "ss", None, ACCENT)
d.edge("ss", "model", None, ACCENT)
d.port("idb.l1", 1362, 170)
d.edge("model", "idb.l1", "ModelDto", via=[(1260, 240), (1260, 170)], label_at=0.8, label_dy=-6)
d.port("idb.b1", 1390, 222)
d.edge("idb.b1", "cloud", "push / pull", ASYNC, dashed=True, both=True, label_dx=6, anchor="start")
d.port("idb.t1", 1390, 132)
d.edge("idb.t1", "ms", "the cached model, opened first", dashed=True, via=[(1390, 110), (770, 110)], label_at=0.5, label_dy=-5)

d.group(20, 470, 1460, 250, "3 · Render and interact", "per zoom level, only what is on screen")
d.node("layout", 250, 580, "Configuration", "Layout", "NodeLayout + LayeredEngine:\na container is laid out when\nit first opens; idle refinement")
d.node("rep", 520, 580, "Network", "RepLineService", "which lines show at this zoom:\naggregated funnels, split\nwhere the user asks")
d.node("svg", 790, 580, "View", "SvgService", "renders SVG tiles, cached\nin TileCache (LRU, byte budget)")
d.node("canvas", 1060, 580, "View", "Canvas", "Blazor component +\njsInterop.js pointer events")
d.person("user", 1340, 585, "User", "pan, zoom, select,\nedit, explore")
d.port("model.b1", 1160, 320)
d.edge("model.b1", "layout", "the model", ACCENT, via=[(1160, 455), (250, 455)], label_at=0.2, label_dx=6, anchor="start")
d.edge("layout", "rep", None, ACCENT)
d.edge("rep", "svg", None, ACCENT)
d.edge("svg", "canvas", "tiles", ACCENT, label_dy=-8)
d.edge("user", "canvas", None, ACCENT, both=True)
d.note(1200, 690, ["Edits and analysis results show", "in the next render of the tiles."], size=10.5)

d.group(20, 750, 1460, 190, "Features that read or change the same model", "Diagrams/ and Modeling/Commands/")
feats = [("explorer", "Search", "Dependency explorer", "what a node uses,\nand what uses it"),
         ("cycles", "Analytics", "Cycles", "strongly connected sets of\nsiblings; accept or fix"),
         ("rules", "Security", "Architecture rules", "\"X must not depend on Y\",\nviolations highlighted"),
         ("paths", "Network", "Path finder", "why does A depend on B:\nshortest chains"),
         ("problems", "Logging", "Problem indicator", "compile errors, open cycles,\nbroken rules"),
         ("search", "Search", "Search, share links", "fuzzy node search, links\nto a node or a view"),
         ("commands", "Utility", "CommandService", "every edit is a command:\nundo / redo, then saved")]
for i, (key, icon, label, sub) in enumerate(feats):
    d.node(key, 120 + i * 205, 835, icon, label, sub, size=36)
write("4-runtime.svg", d, "Runtime view of Dependinator: a parser (Roslyn for C#, the Bicep compiler for Bicep, or a pre-parsed demo file) turns source into a flat list of items; across the IParserService RPC boundary ModelService and StructureService build the model of nodes, links and lines, which is cached in IndexedDB and optionally synced; the layout engine, RepLineService and SvgService turn it into cached SVG tiles on the canvas; the explorer, cycles, rules, path finder, problem indicator, search and undoable commands work on the same model.")

# ------------------------------------------------------------------------------------------
# 5. VS Code integration view: how the webview, the extension and the server talk
# ------------------------------------------------------------------------------------------
d = Diagram(1440, 790, "Dependinator VS Code integration view")
d.group(20, 60, 380, 700, "Webview", "Blazor WebAssembly: Dependinator.Wasm + UI")
d.box("ui", 50, 110, 320, 110, "UI services", [
    "ModelService, cloud sync, Claude prompts,", "workspace model list: they call [Rpc]", "interfaces (IParserService, ...) as proxies"], icon="View", tint=ACCENT)
d.box("rpc1", 50, 270, 320, 110, "JsonRpcService (Core)", [
    "StreamJsonRpc + System.Text.Json;", "gzip from 64 KB, chunks of at most 1 MB,", "base64 for the string-only transports"], icon="Communication")
d.box("bridge", 50, 430, 320, 90, "VsCodeMessageService", [
    "postMessage bridge (jsInterop.js),", "only active inside a VS Code webview"], icon="Messaging")
d.box("sendrecv", 50, 570, 320, 160, "VsCodeSend / ReceiveService", [
    "to the extension: open the editor at a", "  node, parsing status, diagram loaded,", "  clipboard, help",
    "from the extension: show the node of the", "  active editor, refresh, open search"], icon="Messaging")
d.edge("ui.b", "rpc1.t", None, ACCENT, both=True)
d.edge("rpc1.b", "bridge.t", None, ACCENT, both=True)

d.group(540, 60, 380, 700, "Extension host", "TypeScript, Node.js")
d.box("ls", 570, 110, 320, 100, "languageServer.ts", [
    "starts server/<rid>/Dependinator.Lsp", "(dotnet run in development);", "restarted if it crashes"], icon="Configuration")
d.box("ext", 570, 330, 320, 195, "extension.ts", [
    "relays lsp/message (UI → server)", "  and ui/message (server → UI)",
    "holds UI messages until ui/lspReady", "opens files at a node's source",
    "follows the active editor (debounced)", "file watcher → ui/refresh", "status bar: parsing / idle",
    "commands: Open, Search, Refresh, ..."], icon="Utility")
d.box("secrets", 570, 610, 320, 110, "VS Code SecretStorage", [
    "Clerk access token (cloudSyncAuth.ts)", "Claude API key (claudeApiKey.ts)", "never stored in the UI"], icon="Security")

d.group(1040, 60, 380, 700, "Language server", "Dependinator.Lsp, .NET, OmniSharp")
d.box("lmh", 1070, 110, 320, 100, "LspMessageHandler", ["notification lsp/message in,", "notification ui/message out"], icon="Messaging")
d.box("rpc2", 1070, 260, 320, 90, "JsonRpcService (Core)", ["the same transport class as the UI"], icon="Communication")
d.box("targets", 1070, 400, 320, 190, "[Rpc] targets", [
    "IParserService → Roslyn or Bicep parser", "IWorkspaceFileService: the workspace's", "  .sln files and Bicep entry points,", "  source text for excerpts",
    "ICloudSyncRpcService → sync API (HTTP)", "IClaudeService → Anthropic API"], icon="Parsing", tint=ACCENT)
d.box("wfs", 1070, 640, 320, 90, "WorkspaceFolderService", ["workspace folders from initialize", "and folder-change notifications"], icon="Files")
d.edge("lmh.b", "rpc2.t", None, ACCENT, both=True)
d.edge("rpc2.b", "targets.t", None, ACCENT, both=True)

d.port("ext.l1", 570, 475)
d.edge("bridge.r", "ext.l1", "postMessage\n{ lsp/message, base64 }", ACCENT, both=True, label_dy=-20)
d.port("sr.r1", 370, 650); d.port("ext.l2", 570, 505)
d.edge("sr.r1", "ext.l2", "vscode/* and ui/*\nmessages", ASYNC, dashed=True, both=True, via=[(470, 650), (470, 505)], label_at=0.25, label_dy=-18, label_dx=42, anchor="end")
d.port("ls.r1", 890, 150); d.port("lmh.l1", 1070, 150)
d.edge("ls.r1", "lmh.l1", "spawns", ASYNC, dashed=True, label_dy=-6)
d.port("ext.r1", 890, 420); d.port("lmh.l2", 1070, 185)
d.edge("ext.r1", "lmh.l2", "LSP notifications\nover stdio", ACCENT, both=True, via=[(980, 420), (980, 185)], label_at=0.6, label_dx=-6, anchor="end")
d.edge("ext.t", "ls.b", "starts", label_dx=6, anchor="start", label_dy=4)
d.port("targets.l1", 1070, 560); d.port("secrets.r1", 890, 665)
d.edge("targets.l1", "secrets.r1", "asks for the token or\nthe key, per request", ASYNC, dashed=True, via=[(1000, 560), (1000, 665)], label_at=0.667, label_dy=-20, label_dx=-8, anchor="end")
write("5-vscode.svg", d, "VS Code integration view: the UI in the webview calls [Rpc] interfaces through Core's JsonRpcService, whose messages go via postMessage to the extension host, which relays them as LSP notifications over stdio to the language server, where another JsonRpcService dispatches them to the parser, workspace file, cloud sync and Claude services. The extension also sends editor, refresh and search messages to the UI, starts the server, and keeps the Clerk token and Claude key in VS Code's secret storage.")

# ------------------------------------------------------------------------------------------
# 6. Cloud sync and identity view
# ------------------------------------------------------------------------------------------
d = Diagram(1400, 760, "Dependinator cloud sync and identity view")
d.group(20, 60, 400, 230, "Browser app", "dependinator.com")
d.box("bui", 50, 105, 340, 160, "Dependinator.UI", [
    "AppCloudSyncService: compares the local", "and the cloud copy by content hash,",
    "pushes local edits, pulls newer copies", "HttpCloudSyncService: calls /api",
    "Clerk.js: sign-in in the page"], icon="View", tint=ACCENT)

d.group(20, 330, 400, 400, "VS Code", "the same UI, another transport")
d.box("vui", 50, 375, 340, 90, "Dependinator.UI (webview)", ["the same AppCloudSyncService; calls go", "over the JSON-RPC tunnel (view 5)"], icon="View", tint=ACCENT)
d.box("vlsp", 50, 505, 340, 90, "CloudSyncRpcService (language server)", ["makes the HTTP calls with", "Core's CloudSyncHttpClient"], icon="Parsing")
d.box("vext", 50, 635, 340, 80, "cloudSyncAuth.ts (extension)", ["loopback sign-in page with Clerk.js,", "token kept in SecretStorage"], icon="Security")
d.edge("vui.b", "vlsp.t", "JSON-RPC", ACCENT, label_dx=6, anchor="start", label_dy=4)
d.edge("vlsp.b", "vext.t", "token, sign-in", ASYNC, dashed=True, label_dx=6, anchor="start", label_dy=4, both=True)

d.node("clerk", 640, 130, "Authentication", "Clerk", "hosted sign-in; issues the JWT,\npublishes its signing keys")
d.group(820, 200, 560, 530, "Azure Static Web Apps · /api", "src/Api, Azure Functions (isolated worker)")
d.box("auth", 850, 250, 300, 105, "CloudSyncBearerTokenValidator", ["checks the JWT's signature against Clerk's", "keys (JWKS) and its issuer; the user id", "comes from the token"], icon="Security")
d.box("fns", 850, 400, 300, 150, "Functions", ["GET /api/auth/me", "GET /api/models", "GET /api/models/{key}", "PUT /api/models/{key}", "DELETE /api/models/{key}"], icon="Azure-Functions")
d.node("blob", 1285, 475, "Blob-Storage", "Blob Storage", "dependinator-models/<user>/<key>\ngzipped model; hash and time\nin the blob metadata")
d.note(850, 600, ["**What the API enforces",
                  "· every call needs a valid token; users only reach their own folder",
                  "· at most 10 MB of models per user (CloudSyncOptions)",
                  "· a model's key is its normalized path, the same on every device",
                  "· tokens count as valid for 180 days after issue: a workaround for",
                  "  the 7-day limit of Clerk's free tier"], size=11)

d.port("bui.r1", 390, 120); d.port("clerk.l1", 612, 120)
d.edge("bui.r1", "clerk.l1", "sign in (Clerk.js)", label_dy=-6)
d.port("vext.r1", 390, 690); d.port("clerk.l2", 612, 142)
d.edge("vext.r1", "clerk.l2", "sign in (loopback page)", via=[(560, 690), (560, 142)], label_at=0.33, label_dy=-6, label_dx=-6, anchor="end")
d.port("bui.r2", 390, 225); d.port("fns.l1", 850, 455)
d.edge("bui.r2", "fns.l1", "Bearer JWT", ACCENT, via=[(720, 225), (720, 455)], label_at=0.1, label_dy=-6)
d.port("vlsp.r1", 390, 550); d.port("fns.l2", 850, 505)
d.edge("vlsp.r1", "fns.l2", "Bearer JWT", ACCENT, via=[(760, 550), (760, 505)], label_at=0.1, label_dy=-6)
d.port("auth.t1", 1120, 250)
d.edge("auth.t1", "clerk", "signing keys (JWKS), cached", dashed=True, via=[(1120, 130)], label_at=0.75, label_dy=-6)
d.edge("fns.t", "auth.b", "who is calling?", label_dx=6, anchor="start", label_dy=4)
d.edge("fns.r", "blob", "list, read,\nwrite, delete", ACCENT, label_dy=-20)
d.note(40, 752, ["Both clients send the token in Authorization and in X-Dependinator-Authorization, since Static Web Apps may drop the standard header before the Functions see it."], size=10.5)
write("6-cloud-sync.svg", d, "Cloud sync and identity view: users sign in with Clerk (Clerk.js in the browser app, a loopback sign-in page from the VS Code extension); the browser UI calls the Functions API with a Bearer token, while in VS Code the language server calls it with the token the extension keeps in secret storage. The API validates the token against Clerk's signing keys and reads and writes the user's gzipped models in Blob Storage, at most 10 MB per user.")

# ------------------------------------------------------------------------------------------
# 7. Delivery view: from a commit to production and the Marketplace
# ------------------------------------------------------------------------------------------
d = Diagram(1440, 660, "Dependinator delivery view")
d.box("feature", 30, 70, 230, 110, "Feature branch", ["mr/<what-the-work-is>, from dev;", "each push: unit tests + e2e", "in Chromium"], icon="Utility")
d.box("dependabot", 30, 240, 230, 90, "Dependabot", ["package updates as PRs to dev", "(security PRs: merge into dev)"], icon="Security")
d.box("devb", 330, 70, 240, 260, "dev", [
    "**the integration branch", "each push: unit tests + e2e", "in Chromium; nothing is", "deployed by a push or a merge",
    "holds the notes of the next", "release: CHANGELOG.md's", "## [Unreleased]"], icon="Testing", tint=ACCENT)
d.box("release", 640, 40, 230, 175, "release.yml, run by hand", ["**a preview or a release", "the full gate: unit tests, e2e", "in 3 browsers, cloud sync e2e;",
    "builds the site and one .vsix", "per platform (Linux, Windows,", "macOS; x64 and arm64)", "then pushes and publishes"], icon="Configuration")
d.box("main", 640, 270, 230, 100, "main", ["**releases only", "the workflow merges dev into it", "and raises the minor on dev"], icon="Static-Web-Apps", tint=ACCENT)
d.node("site", 1000, 305, "Static-Web-Apps", "dependinator.com", "previews: the dev site")
d.box("vsx", 1100, 220, 310, 175, "GitHub release v<version>", [
    "tagged on what was built:", "main's merge, or dev for a preview", "(a pre-release)",
    "the .vsix packages as assets", "and the notes from CHANGELOG.md"], icon="Files")
d.node("market", 1255, 500, "Cloud", "VS Code Marketplace", "releases only")
d.port("devb.l1", 330, 125); d.port("devb.l2", 330, 285)
d.edge("feature.r", "devb.l1", "PR, merge", ACCENT, label_dy=-8)
d.edge("dependabot.r", "devb.l2", "PR", label_dy=-8)
d.port("devb.r1", 570, 125)
d.edge("devb.r1", "release.l", "run on dev", ACCENT, label_dy=-8)
d.edge("release.b", "main.t", "release", ACCENT)
d.edge("main.r", "site", "deploy", ACCENT, label_dy=-8)
d.port("vsx.l1", 1100, 305)
d.edge("site", "vsx.l1", "then", ACCENT, label_dy=-8)
d.edge("vsx.b", "market", None, ACCENT)
d.note(30, 595, ["**Quality gate",
                 "About 900 unit tests and a Playwright suite of 4.4k lines run on every push (build.yml calls unit-tests.yml and e2e.yml), and the Release workflow runs them all before it publishes;",
                 "Playwright traces are uploaded when an e2e test fails. CSharpier formatting is checked during the build, and the architecture tests guard the layering."], size=11)
write("7-delivery.svg", d, "Delivery view of Dependinator: feature branches and Dependabot PRs merge into dev, whose pushes only run the tests; the Release workflow, run by hand on dev, runs the full gate and builds the site and a .vsix per platform, and then a preview deploys the pre-release site and a GitHub pre-release, while a release merges dev into main, deploys dependinator.com and publishes the VS Code Marketplace and a GitHub release.")

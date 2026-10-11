"""Generates the webshop architecture diagrams (examples/bicep/ARCHITECTURE.md) as self-contained
SVGs: hand-placed nodes with the official Azure icons from Dependinator's icon library, groups and
labeled edges. Strokes and text use currentColor and fills use CSS variables with light fallbacks,
so the files render standalone (GitHub, image viewers) and follow the theme when inlined in a page.

Run from anywhere: python3 examples/bicep/architecture/generate.py"""
import re, html, pathlib

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[2]
ICON_DIR = REPO / "src/Dependinator.UI/Diagrams/Icons/Library"
ACCENT = "var(--accent, #2563eb)"     # synchronous request path (themeable when inlined)
ASYNC = "var(--accent2, #b45309)"     # events / messages
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

class Diagram:
    def __init__(self, w, h, title):
        self.w, self.h, self.title = w, h, title
        self.icons = set()
        self.body = []
        self.nodes = {}

    def group(self, x, y, w, h, title, sub=None, dashed=False, tint=None):
        dash = ' stroke-dasharray="6 4"' if dashed else ""
        fill = f'fill="{tint}" fill-opacity="0.08"' if tint else 'fill="currentColor" fill-opacity="0.035"'
        self.body.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="10" {fill} stroke="currentColor" stroke-opacity="0.35"{dash}/>')
        self.body.append(f'<text x="{x+12}" y="{y+20}" font-size="13" font-weight="600" fill="currentColor">{html.escape(title)}</text>')
        if sub:
            self.body.append(f'<text x="{x+12}" y="{y+36}" font-size="11" fill="currentColor" fill-opacity="0.7">{html.escape(sub)}</text>')

    def node(self, key, x, y, icon, label, sub=None, size=44, dashed=False):
        """Icon centered at (x, y) with the label below. Registers the node's box for edges."""
        self.icons.add(icon)
        half = size / 2
        self.body.append(f'<use href="#ic-{icon}" x="{x-half}" y="{y-half}" width="{size}" height="{size}"/>')
        if dashed:
            self.body.append(f'<rect x="{x-half-6}" y="{y-half-6}" width="{size+12}" height="{size+12}" rx="8" fill="none" stroke="currentColor" stroke-opacity="0.5" stroke-dasharray="4 3"/>')
        ty = y + half + 14
        for i, line in enumerate(label.split("\n")):
            self.body.append(f'<text x="{x}" y="{ty + i*13}" font-size="12" font-weight="600" text-anchor="middle" fill="currentColor">{html.escape(line)}</text>')
        n = label.count("\n") + 1
        if sub:
            for i, line in enumerate(sub.split("\n")):
                self.body.append(f'<text x="{x}" y="{ty + n*13 + i*12}" font-size="10.5" text-anchor="middle" fill="currentColor" fill-opacity="0.72">{html.escape(line)}</text>')
        self.nodes[key] = (x, y, half + 6)   # center and "radius" used to stop edges at the icon

    def person(self, key, x, y, label, sub=None):
        self.body.append(f'<circle cx="{x}" cy="{y-10}" r="9" fill="none" stroke="currentColor" stroke-width="1.6"/>')
        self.body.append(f'<path d="M {x-16} {y+16} a 16 14 0 0 1 32 0" fill="none" stroke="currentColor" stroke-width="1.6"/>')
        ty = y + 30
        self.body.append(f'<text x="{x}" y="{ty}" font-size="12" font-weight="600" text-anchor="middle" fill="currentColor">{html.escape(label)}</text>')
        if sub:
            self.body.append(f'<text x="{x}" y="{ty+12}" font-size="10.5" text-anchor="middle" fill="currentColor" fill-opacity="0.72">{html.escape(sub)}</text>')
        self.nodes[key] = (x, y, 26)

    def note(self, x, y, lines, w=None, size=11):
        for i, line in enumerate(lines):
            bold = line.startswith("**")
            text = line.strip("*")
            weight = ' font-weight="600"' if bold else ""
            self.body.append(f'<text x="{x}" y="{y + i*15}" font-size="{size}"{weight} fill="currentColor" fill-opacity="{1 if bold else 0.8}">{html.escape(text)}</text>')

    def edge(self, a, b, label=None, color=None, dashed=False, via=None, label_at=0.5, label_dy=-6, label_dx=0, anchor="middle"):
        """Edge from node a to node b through optional waypoints; the ends stop at the icons' edges."""
        ax, ay, ar = self.nodes[a]
        bx, by, br = self.nodes[b]
        pts = [(ax, ay)] + list(via or []) + [(bx, by)]
        # trim first and last segments to the node radius
        def trim(p, q, r):
            dx, dy = q[0]-p[0], q[1]-p[1]
            d = (dx*dx + dy*dy) ** 0.5 or 1
            return (p[0] + dx/d*r, p[1] + dy/d*r)
        pts[0] = trim(pts[0], pts[1], ar)
        pts[-1] = trim(pts[-1], pts[-2], br)
        d = "M " + " L ".join(f"{x:.1f} {y:.1f}" for x, y in pts)
        stroke = color or "currentColor"
        dash = ' stroke-dasharray="6 4"' if dashed else ""
        marker = "arrow-accent" if color == ACCENT else "arrow-async" if color == ASYNC else "arrow"
        self.body.append(f'<path d="{d}" fill="none" stroke="{stroke}" stroke-width="1.5"{dash} marker-end="url(#{marker})"/>')
        if label:
            # label on the longest segment
            segs = [((pts[i][0]+pts[i+1][0])/2, (pts[i][1]+pts[i+1][1])/2, ((pts[i][0]-pts[i+1][0])**2 + (pts[i][1]-pts[i+1][1])**2) ** 0.5) for i in range(len(pts)-1)]
            if label_at == "longest":
                mx, my, _ = max(segs, key=lambda s: s[2])
            else:
                i = min(int(label_at * (len(pts)-1)), len(pts)-2)
                t = label_at * (len(pts)-1) - i
                mx = pts[i][0] + (pts[i+1][0]-pts[i][0]) * t
                my = pts[i][1] + (pts[i+1][1]-pts[i][1]) * t
            fill = color or "currentColor"
            for j, line in enumerate(label.split("\n")):
                self.body.append(f'<text x="{mx + label_dx:.1f}" y="{my + label_dy + j*12:.1f}" font-size="10.5" text-anchor="{anchor}" fill="{fill}" style="paint-order:stroke;stroke:{BG};stroke-width:3px;stroke-linejoin:round">{html.escape(line)}</text>')

    def render(self, aria):
        defs = "".join(icon_symbol(i) for i in sorted(self.icons))
        markers = "".join(
            f'<marker id="{mid}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse"><path d="M 0 0 L 10 5 L 0 10 z" fill="{c}"/></marker>'
            for mid, c in (("arrow", "currentColor"), ("arrow-accent", ACCENT), ("arrow-async", ASYNC)))
        return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {self.w} {self.h}" role="img" aria-label="{html.escape(aria)}" '
                f'font-family="Segoe UI, Inter, system-ui, sans-serif" style="max-width:100%;height:auto;color:var(--fg,#1f2937)">'
                f'<title>{html.escape(self.title)}</title><defs>{markers}{defs}</defs>'
                f'<rect width="{self.w}" height="{self.h}" fill="{BG}"/>' + "".join(self.body) + "</svg>")

OUT = HERE

def write(name, d, aria):
    (OUT / name).write_text(d.render(aria))
    print("wrote", name, len(d.render(aria)) // 1024, "KB")


# ================================================================================================
# The diagrams
# ================================================================================================

# ------------------------------------------------------------------------------------------
# 1. Runtime view: request path and event flows
# ------------------------------------------------------------------------------------------
d = Diagram(1420, 1010, "Webshop runtime view")
d.group(30, 70, 160, 700, "Actors")
d.group(230, 70, 190, 700, "Edge", "rg-apps")
d.group(470, 70, 290, 700, "Applications", "rg-apps")
d.group(1090, 70, 300, 700, "Data & AI", "rg-data · rg-apps")
d.group(470, 800, 920, 170, "Messaging & background processing", "rg-data · rg-apps")

d.person("shopper", 110, 300, "Shopper", "browser, mobile app")
d.person("partner", 110, 700, "Partner systems", "order event webhooks")
d.node("fd", 325, 300, "Front-Door", "Front Door Premium", "WAF policy\nshop.contoso-webshop.example")
d.node("apim", 325, 500, "API-Management", "API Management", "subscription keys\nrate limiting")
d.node("storefront", 600, 150, "App-Service", "Storefront", "App Service (Linux)")
d.node("catalogApi", 600, 330, "App-Service", "Catalog API", "App Service + staging slot")
d.node("checkout", 600, 500, "Container-Apps", "Checkout", "Container App, external ingress")
d.node("catalogSvc", 700, 650, "Container-Apps", "Catalog service", "Container App")
d.node("redis", 1240, 120, "Cache-for-Redis", "Cache for Redis", "sessions, catalog cache")
d.node("storage", 1240, 220, "Storage-Account", "Storage account", "product-images, invoices,\nqueues, sessions table")
d.node("kv", 1240, 330, "Key-Vault", "Key Vault", "payment provider key")
d.node("search", 1240, 430, "AI-Search", "AI Search", "catalog index")
d.node("openai", 1240, 530, "Azure-OpenAI", "Azure OpenAI", "gpt-4o-mini, embeddings")
d.node("cosmos", 1240, 630, "Cosmos-DB", "Cosmos DB", "carts, product-views")
d.node("sql", 1240, 730, "SQL-Database", "Azure SQL", "catalog, orders")
d.node("sb", 600, 870, "Service-Bus", "Service Bus Premium", "queues: orders, payments, thumbnails\ntopic: order-events → fulfilment,\nnotifications, analytics")
d.node("orders", 900, 870, "Azure-Functions", "Order processor", "Functions, Elastic Premium\nService Bus triggered")
d.node("eg", 1240, 870, "Event-Grid", "Event Grid", "system topic: blob created\ncustom topic: partner events")

# synchronous request path
d.edge("shopper", "fd", "HTTPS", ACCENT)
d.edge("fd", "storefront", "route /", ACCENT, via=[(440, 300), (440, 150)], label_at=0.85)
d.edge("fd", "apim", "route /api/*", ACCENT, label_dy=-8)
d.edge("apim", "catalogApi", "catalog API", ACCENT, via=[(440, 500), (440, 330)], label_at=0.85)
d.edge("apim", "checkout", "checkout API", ACCENT)
d.edge("storefront", "catalogApi", "REST", ACCENT, label_dy=4)
d.edge("checkout", "catalogSvc", "REST: prices, stock", ACCENT, via=[(700, 500)], label_at=0.35)
# data access (labels sit on the last horizontal segment, next to the target)
d.edge("storefront", "redis", "sessions", via=[(900, 150), (900, 120)], label_at=0.3)
d.edge("storefront", "storage", "product images", via=[(880, 150), (880, 220)], label_at=0.5, label_dy=14)
d.edge("catalogApi", "kv", "payment key (Key Vault reference)", via=[(920, 330)], label_at=0.33)
d.edge("catalogApi", "search", "search, index updates", via=[(940, 330), (940, 430)], label_at=0.86, label_dy=-6)
d.edge("catalogApi", "openai", "product assistant", via=[(960, 330), (960, 530)], label_at=0.86, label_dy=-6)
d.edge("catalogApi", "sql", "catalog DB", via=[(980, 330), (980, 730)], label_at=0.88, label_dy=-6)
d.edge("catalogSvc", "sql", "catalog DB (read)", via=[(1000, 650), (1000, 730)], label_at=0.3, label_dy=-6)
d.edge("checkout", "cosmos", "carts", via=[(1020, 500), (1020, 630)], label_at=0.4)
# messaging
d.edge("checkout", "sb", "send: orders, payments", ASYNC, dashed=True, label_at=0.64, label_dy=4)
d.edge("sb", "orders", "trigger: orders, payments", ASYNC, dashed=True)
d.edge("orders", "sql", "orders DB", ASYNC, dashed=True, via=[(900, 790), (1240, 790)], label_at=0.6, label_dy=14)
d.edge("orders", "cosmos", "product-views", ASYNC, dashed=True, via=[(900, 810), (1320, 810), (1320, 630)], label_at=0.45, label_dy=14)
d.edge("storage", "eg", "blob created", ASYNC, dashed=True, via=[(1340, 220), (1340, 870)], label_at=0.9, label_dy=14)
d.edge("eg", "sb", "thumbnails queue", ASYNC, dashed=True, via=[(1240, 952), (600, 952)], label_at=0.5, label_dy=14)
d.edge("eg", "partner", "order events (webhooks)", ASYNC, dashed=True, via=[(1240, 990), (20, 990), (20, 700)], label_at=0.35, label_dy=-6)
d.note(40, 790, ["Solid blue: synchronous HTTPS request path.", "Dashed brown: queues and events.", "Every data service is reached over private endpoints;", "only Front Door, API Management and Checkout have public endpoints."], size=11)
write("1-runtime.svg", d, "Runtime view of the webshop: shoppers reach Front Door, which routes to the storefront and through API Management to the catalog and checkout APIs; those use SQL, Cosmos DB, Redis, storage, Key Vault, AI Search and OpenAI; orders flow through Service Bus to the order processor, and blob uploads raise Event Grid events.")

# ------------------------------------------------------------------------------------------
# 2. Deployment view: what webshop.bicep deploys where
# ------------------------------------------------------------------------------------------
d = Diagram(1500, 720, "Webshop deployment view")
d.group(20, 20, 1460, 690, "Subscription", "webshop.bicep, targetScope = subscription · one deployment per environment (dev, test, prod)")
d.group(50, 70, 380, 560, "rg-webshop-{env}-platform", "shared services")
d.node("la", 110, 150, "Log-Analytics", "Log Analytics", "30 / 90 days retention")
d.node("ai", 240, 150, "Application-Insights", "App Insights", "workspace-based")
d.node("alerts", 370, 150, "Monitor", "Alerts", "action group,\n2 metric alerts")
d.node("idApps", 110, 290, "Managed-Identities", "id-apps", "user-assigned")
d.node("idJobs", 240, 290, "Managed-Identities", "id-jobs", "user-assigned")
d.node("kv", 370, 290, "Key-Vault", "Key Vault", "RBAC, purge\nprotection")
d.node("vnet", 110, 430, "Virtual-Network", "Virtual network", "4 delegated\nsubnets")
d.node("nsg", 240, 430, "Network-Security-Group", "2 NSGs", "apps, private\nendpoints")
d.node("nat", 370, 430, "NAT-Gateway", "NAT gateway", "+ static\npublic IP")
d.node("pdns", 110, 560, "Azure-DNS", "Private DNS", "10 privatelink\nzones")
d.note(190, 552, ["linked to the VNet;", "one zone per private-link service"], size=10.5)

d.group(480, 70, 480, 560, "rg-webshop-{env}-data", "data stores and messaging")
d.node("sql", 540, 150, "SQL-Server", "SQL server", "Entra-only auth")
d.node("sqldb", 660, 150, "SQL-Database", "2 databases", "catalog, orders")
d.node("st", 780, 150, "Storage-Account", "Storage account", "blob, queue, table")
d.node("cosmos", 900, 150, "Cosmos-DB", "Cosmos DB", "serverless")
d.node("redis", 540, 290, "Cache-for-Redis", "Redis", "Standard C1 / C2")
d.node("sb", 660, 290, "Service-Bus", "Service Bus", "Premium")
d.node("eg", 780, 290, "Event-Grid", "Event Grid", "system topic,\ncustom topic")
d.node("pe", 900, 290, "Private-Endpoint", "7 private\nendpoints", "shared module")
d.node("rbac", 540, 430, "Role-Assignment", "Role\nassignments", "data-plane roles")
d.node("lock", 660, 430, "Azure-Policy", "Lock (prod)", "CanNotDelete\non the SQL server")
d.node("diag", 780, 430, "Monitor", "Diagnostics", "to Log Analytics")

d.group(1010, 70, 460, 470, "rg-webshop-{env}-apps", "applications and edge")
d.node("plan", 1070, 150, "App-Service-Plan", "App Service\nplan", "Linux, B1 / P1v3")
d.node("web", 1180, 150, "App-Service", "2 web apps", "storefront,\ncatalog API")
d.node("func", 1290, 150, "Azure-Functions", "Function app", "order processor")
d.node("fst", 1400, 150, "Storage-Account", "Functions\nstorage", "identity-based")
d.node("cae", 1070, 290, "Container-Apps", "Container Apps", "environment,\ncatalog, checkout")
d.node("acr", 1180, 290, "Container-Registry", "Container\nregistry", "Premium, private")
d.node("apim", 1290, 290, "API-Management", "API\nManagement", "2 APIs, 1 product")
d.node("fd", 1400, 290, "Front-Door", "Front Door", "Premium + WAF")
d.node("srch", 1070, 430, "AI-Search", "AI Search", "deployAi only", dashed=True)
d.node("oai", 1180, 430, "Azure-OpenAI", "Azure OpenAI", "deployAi only", dashed=True)
d.node("pe2", 1290, 430, "Private-Endpoint", "3 private\nendpoints", "registry, search,\nopenai")
d.node("budget", 1400, 430, "Subscription", "Budget (prod)", "subscription\nscope")

d.group(1010, 560, 460, 110, "rg-shared-dns (existing)", "not created by this deployment")
d.node("dns", 1060, 625, "Azure-DNS", "", None, size=34)
d.note(1100, 618, ["**contoso-webshop.example", "CNAME shop → Front Door endpoint,", "TXT _dnsauth.shop for the managed certificate"], size=10.5)

d.nodes["rgPlatformEdge"] = (430, 350, 0); d.nodes["rgDataEdge"] = (480, 350, 0)
d.nodes["rgDataEdgeR"] = (960, 220, 0); d.nodes["rgAppsEdge"] = (1010, 220, 0)
d.edge("rgPlatformEdge", "rgDataEdge", "subnet ids,\nDNS zone ids,\nworkspace id", label_at=0.5, label_dy=-32)
d.edge("rgDataEdgeR", "rgAppsEdge", "endpoints,\nqueue ids", label_at=0.5, label_dy=-22)
d.nodes["pBottom"] = (240, 630, 0); d.nodes["aBottom"] = (1040, 540, 0)
d.edge("pBottom", "aBottom", "identities, Key Vault, subnets, App Insights connection string", via=[(240, 675), (985, 675), (985, 555), (1040, 555)], label_at=0.3, label_dy=-6)
d.nodes["appsBottom"] = (1250, 540, 0); d.nodes["dnsTop"] = (1250, 560, 0)
d.edge("appsBottom", "dnsTop", "host name, validation token", label_at=0.5, label_dy=-14)
write("2-deployment.svg", d, "Deployment view of the webshop: one subscription-scope deployment creates a platform resource group with monitoring, identities, Key Vault and the network, a data resource group with SQL, storage, Cosmos DB, Redis, Service Bus and Event Grid, and an apps resource group with App Service, Functions, Container Apps, API Management, Front Door and the optional AI services; module outputs flow from platform to data to apps, and DNS records land in an existing shared zone.")

# ------------------------------------------------------------------------------------------
# 3. Network view
# ------------------------------------------------------------------------------------------
d = Diagram(1400, 800, "Webshop network view")
d.node("internet", 90, 110, "Cloud", "Internet", "shoppers, partners")
d.node("fd", 330, 110, "Front-Door", "Front Door Premium", "WAF, managed certificate")
d.node("apim", 600, 110, "API-Management", "API Management", "public gateway\n(virtualNetworkType: None)")
d.group(230, 230, 790, 540, "vnet-webshop-{env} · 10.40.0.0/16", "rg-platform")
d.group(250, 270, 370, 210, "snet-apps · 10.40.1.0/24", "delegated to Microsoft.Web/serverFarms")
d.node("plan", 350, 370, "App-Service-Plan", "App Service plan", "storefront, catalog API\nregional VNet integration")
d.node("nsgApps", 540, 360, "Network-Security-Group", "NSG", "allow 443 from\nAzureFrontDoor.Backend", size=36)
d.group(640, 270, 360, 210, "snet-container-apps · 10.40.2.0/23", "delegated to Microsoft.App/environments")
d.node("cae", 780, 370, "Container-Apps", "Container Apps environment", "catalog, checkout\nconsumption workload profile")
d.group(250, 500, 370, 250, "snet-integration · 10.40.5.0/24", "delegated to Microsoft.Web/serverFarms")
d.node("func", 350, 610, "Azure-Functions", "Function app", "order processor\nregional VNet integration")
d.group(640, 500, 360, 250, "snet-private-endpoints · 10.40.4.0/24", "NSG: VNet inbound only · network policies off")
pes = ["vault", "sql", "blob", "queue", "cosmos", "servicebus", "redis", "registry", "search", "openai"]
for i, name in enumerate(pes):
    x = 690 + (i % 5) * 62
    y = 600 if i < 5 else 690
    d.node(f"pe-{name}", x, y, "Private-Endpoint", name, None, size=30)
d.node("pip", 1120, 290, "Public-IP", "Public IP", "static, zone-redundant")
d.node("nat", 1120, 410, "NAT-Gateway", "NAT gateway", "outbound for apps,\ncontainer apps, integration")
d.node("pdns", 1120, 560, "Azure-DNS", "Private DNS zones", "privatelink.* for all\n10 services")
d.group(1190, 230, 200, 540, "Private-link PaaS", "public access disabled")
for i, (key, icon, label) in enumerate([("kv", "Key-Vault", "Key Vault"), ("sql", "SQL-Server", "SQL server"), ("st", "Storage-Account", "Storage"),
                                        ("cosmos", "Cosmos-DB", "Cosmos DB"), ("sb", "Service-Bus", "Service Bus"), ("redis", "Cache-for-Redis", "Redis"),
                                        ("acr", "Container-Registry", "Registry"), ("srch", "AI-Search", "AI Search"), ("oai", "Azure-OpenAI", "OpenAI")]):
    x = 1245 + (i % 2) * 90
    y = 300 + (i // 2) * 100
    d.node(key, x, y, icon, label, None, size=32)
d.edge("internet", "fd", "HTTPS 443", ACCENT)
d.edge("fd", "plan", "origin: storefront\n(Front Door service tag only)", ACCENT, via=[(330, 370)], label_at=0.125, label_dy=-6, label_dx=-62)
d.edge("fd", "apim", "/api/*", ACCENT)
d.edge("apim", "plan", "catalog API (public endpoint)", ACCENT, via=[(600, 215), (400, 215), (400, 370)], label_at=0.5, label_dy=-6, label_dx=40)
d.edge("apim", "cae", "checkout (external ingress)", ACCENT, via=[(780, 110)], label_at=0.62, label_dy=-6, label_dx=8, anchor="start")
d.nodes["peBox"] = (1000, 700, 0); d.nodes["paasBox"] = (1190, 700, 0)
d.edge("peBox", "paasBox", "private link", label_at=0.5, label_dy=-6)
d.nodes["appsOut"] = (620, 440, 0); d.nodes["caeOut"] = (1000, 320, 0); d.nodes["funcOut"] = (620, 560, 0)
d.edge("caeOut", "nat", "outbound", via=[(1060, 320), (1060, 410)], label_at=0.3, label_dy=-6)
d.edge("funcOut", "nat", "outbound", via=[(630, 560), (630, 490), (1040, 490), (1040, 410)], label_at=0.6, label_dy=-6)
d.edge("appsOut", "nat", None, via=[(630, 440), (630, 490), (1040, 490), (1040, 410)])
d.edge("nat", "pip", None)
d.nodes["vnetDns"] = (1020, 560, 0)
d.edge("pdns", "vnetDns", "zone links", label_at=0.5, label_dy=-6)
d.note(40, 250, ["**Ingress", "Front Door is the only public", "entry for shoppers. The storefront", "accepts the Front Door service tag", "only; the catalog API and checkout", "are published through API", "Management.", "", "**Egress", "All delegated subnets leave through", "the NAT gateway's static IP, which", "partners can allow-list.", "", "**Data plane", "Nine services are reachable only", "through private endpoints in", "snet-private-endpoints; their", "private DNS zones are linked to", "the VNet."], size=10.5)
write("3-network.svg", d, "Network view of the webshop: Internet traffic enters through Front Door and API Management; the virtual network has delegated subnets for App Service, Container Apps and Functions, a private-endpoint subnet for nine PaaS services whose public access is disabled, a NAT gateway for outbound traffic and private DNS zones linked to the network.")

# ------------------------------------------------------------------------------------------
# 4. Identity and security view
# ------------------------------------------------------------------------------------------
d = Diagram(1300, 860, "Webshop identity and security view")
d.group(40, 40, 260, 700, "Workloads", "run as a user-assigned identity")
d.node("storefront", 150, 130, "App-Service", "Storefront")
d.node("catalogApi", 150, 240, "App-Service", "Catalog API", "+ staging slot")
d.node("ca", 150, 360, "Container-Apps", "Checkout, Catalog", "container apps")
d.node("apim", 150, 480, "API-Management", "API Management")
d.node("func", 150, 640, "Azure-Functions", "Order processor")
d.group(380, 40, 300, 700, "Identities", "no secrets in configuration")
d.node("admins", 530, 110, "Role-Assignment", "webshop-platform-admins", "Entra ID group")
d.node("idApps", 530, 350, "Managed-Identities", "id-webshop-apps", "user-assigned")
d.node("idJobs", 530, 620, "Managed-Identities", "id-webshop-jobs", "user-assigned")
d.group(760, 40, 500, 700, "Services", "data-plane access by Azure RBAC only")
targets = [("sql", "SQL-Server", "SQL server", "Entra-only authentication"), ("acr", "Container-Registry", "Registry", "admin user disabled"),
           ("srch", "AI-Search", "AI Search", "Entra or key"), ("kv", "Key-Vault", "Key Vault", "RBAC authorization"),
           ("st", "Storage-Account", "Storage", "shared keys disabled"), ("sb", "Service-Bus", "Service Bus", "local auth disabled"),
           ("cosmos", "Cosmos-DB", "Cosmos DB", "local auth disabled"), ("oai", "Azure-OpenAI", "Azure OpenAI", "local auth disabled")]
for i, (key, icon, label, sub) in enumerate(targets):
    y = 95 + i * 85
    d.node(key, 1040, y, icon, label, sub, size=40)
    d.nodes[f"{key}-a"] = (1012, y - 9, 0)   # arrow port for id-apps
    d.nodes[f"{key}-j"] = (1012, y + 11, 0)  # arrow port for id-jobs
for w in ("storefront", "catalogApi", "ca", "apim"):
    d.edge(w, "idApps", None, ACCENT)
d.edge("func", "idJobs", "runs as", ACCENT, label_dy=-6)
d.note(220, 345, ["runs as"], size=10.5)
d.edge("admins", "sql-a", "server administrator", label_at=0.45, label_dy=-6)
d.edge("admins", "kv-a", "Key Vault Administrator", via=[(720, 110), (720, 350)], label_at=0.42, label_dy=4, label_dx=6, anchor="start")
apps_roles = [("kv", "Secrets User"), ("st", "Blob Data Contributor"), ("sb", "Data Sender"), ("cosmos", "Data Contributor"),
              ("acr", "AcrPull"), ("srch", "Index Data Contributor"), ("oai", "OpenAI User")]
for key, role in apps_roles:
    d.edge("idApps", f"{key}-a", role, label_at=0.7, label_dy=-5, anchor="end", label_dx=-6)
jobs_roles = [("kv", "Secrets User"), ("st", "Queue Data Contributor, Blob Data Owner (host)"), ("sb", "Data Receiver"),
              ("cosmos", "Data Contributor"), ("oai", "OpenAI User")]
for key, role in jobs_roles:
    d.edge("idJobs", f"{key}-j", role, dashed=True, label_at=0.9, label_dy=13, anchor="end", label_dx=-6)
d.note(40, 770, ["**Secrets", "The payment provider key exists only in Key Vault: the Catalog API reads it through a Key Vault reference and API Management through a",
                 "Key Vault named value, both as id-webshop-apps. SQL uses Entra-only authentication; storage, Service Bus, Cosmos DB, OpenAI and the registry have",
                 "their key-based access switched off; TLS 1.2 is the minimum everywhere; the data services accept connections only through private endpoints."], size=11)
write("4-identity.svg", d, "Identity and security view of the webshop: the web apps, container apps and API Management run as one user-assigned identity and the order processor as another; each identity holds the data-plane roles it needs on Key Vault, storage, Service Bus, Cosmos DB, the registry, AI Search and OpenAI, an Entra group administers SQL and Key Vault, and key-based access is disabled on the data services.")

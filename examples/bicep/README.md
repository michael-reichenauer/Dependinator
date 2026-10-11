# Webshop platform (Bicep example)

A fictional e-commerce platform on Azure, written as a realistic multi-file Bicep deployment to
try out and demo Dependinator's Bicep support. It compiles with the Bicep compiler, but it is not
meant to be deployed as-is: names, SKUs, principals, images and the DNS zone are illustrative.

## Open it in Dependinator

- **VS Code:** open this repository (or just this folder) and run **Dependinator: Open**. The
  Models menu lists `webshop.bicep`, the deployment's entry point; the modules it references are
  parsed as part of it. Right-click a resource for **Show Source**, and the diagram follows the
  cursor when you edit a `.bicep` file.
- **Local development host:** `./scripts/watch` lists the same entry point under
  Models › Manage Models › This workspace.

Only entry points are listed: a `.bicep` file that another file uses as a module (everything
under `modules/`) is shown nested inside the module that references it.

## Architecture

[ARCHITECTURE.md](ARCHITECTURE.md) has four review-style diagrams of the platform: the runtime view with the
request and order flows, the deployment view per resource group, the network view and the identity and
security view, each with the points worth discussing.

## What is in it

`webshop.bicep` is a subscription-scope orchestrator. It creates three resource groups and
deploys one module into each, passing outputs between them:

| Area | Module | Resources |
|---|---|---|
| Platform | `platform/monitoring.bicep` | Log Analytics, Application Insights, action group, metric alerts |
| Platform | `platform/identity.bicep` | Two user-assigned managed identities (apps, background jobs) |
| Platform | `platform/network.bicep` | VNet with five delegated subnets, NSGs, NAT gateway, twelve private DNS zones (loop) |
| Platform | `security/keyvault.bicep` | Key Vault (RBAC), secret, role assignments (loop), private endpoint |
| Data | `data/sql.bicep` | SQL server (Entra-only), two databases, auditing, private endpoint |
| Data | `data/storage.bicep` | Storage account, blob/queue/table services and their children, lifecycle, RBAC, three private endpoints |
| Data | `data/cosmos.bicep` | Serverless Cosmos DB account, database, containers, data-plane RBAC, private endpoint |
| Data | `data/redis.bicep` | Azure Cache for Redis, diagnostics, private endpoint |
| Messaging | `messaging/servicebus.bicep` | Premium namespace, queues, topic with subscriptions and a rule, RBAC, private endpoint |
| Messaging | `messaging/events.bicep` | Event Grid system topic (blob events to a queue) and a custom topic |
| Apps | `apps/app-service.bicep` | Linux plan, storefront and API web apps without public endpoints (Private Link origin, private endpoint), staging slot, app settings, diagnostics |
| Apps | `apps/functions.bicep` | Elastic Premium function app for order processing with its storage account |
| Apps | `apps/container-apps.bicep` | Container registry, internal Container Apps environment with its DNS zone (`container-apps-dns.bicep`), catalog and checkout apps |
| Apps | `apps/api-management.bicep` | VNet-integrated API Management (v2) with two APIs, a product, a Key Vault named value, and a Front Door-only policy applied by `api-management-policy.bicep` |
| Edge | `apps/front-door.bicep` | Front Door Premium profile, endpoint, origin groups, routes, custom domain, WAF |
| Edge | `platform/dns.bicep` | CNAME and TXT records in an *existing* public DNS zone |
| AI | `ai/search.bicep`, `ai/openai.bicep` | AI Search and Azure OpenAI with model deployments (conditional on `deployAi`) |
| Shared | `shared/private-endpoint.bicep` | Reused twelve times: a private endpoint plus its DNS zone group |
| Shared | `types.bicep` | Exported user-defined types and a naming function |

The `environments/` folder holds `.bicepparam` files for dev and prod. Parameter files are not
models of their own; they belong to the entry point they `use`.

Things worth looking at in the diagram:

- The resource groups and the modules deployed into them; a module's `scope` is a dependency.
- `shared/private-endpoint.bicep` expanded under every data store that uses it.
- The existing DNS zone under **Externals**, with the deployed records linking to it.
- Parameters, variables and outputs as member rows, and how module parameters receive the
  outputs of other modules (storage endpoints flowing into the web apps, for instance).
- Loops (`privateDnsZones`, role assignments) and conditionals (`deployAi`, the production
  lock), marked in the node descriptions.

## Validating the files

```bash
bicep build examples/bicep/webshop.bicep --stdout > /dev/null   # with the Bicep CLI
dotnet test tests/Dependinator.Bicep.Tests/Dependinator.Bicep.Tests.csproj  # compiles it without errors as part of the parser tests
```

using Bicep.Core.TypeSystem;

namespace Dependinator.Bicep.Parsing;

// Maps Azure resource types to the ids of the Azure icons embedded in the UI's icon library
// (src/Dependinator.UI/Diagrams/Icons/Library/Azure, curated by scripts/cloud-icons/azure.manifest).
// Lookup is case-insensitive and falls back from a child type to its parents
// ("Microsoft.Storage/storageAccounts/blobServices/containers" -> ".../blobServices" -> the account),
// then to a per-provider default, then to the generic (tintable) "Cloud" icon.
static class AzureIcons
{
    public const string Fallback = "Cloud";

    static readonly Dictionary<string, string> ByResourceType = new(StringComparer.OrdinalIgnoreCase)
    {
        // Management & governance
        ["Microsoft.Resources/resourceGroups"] = "Resource-Group",
        ["Microsoft.Resources/deployments"] = "ARM-Template",
        ["Microsoft.Management/managementGroups"] = "Management-Group",
        ["Microsoft.Subscription/aliases"] = "Subscription",
        ["Microsoft.Authorization/roleAssignments"] = "Role-Assignment",
        ["Microsoft.Authorization/roleDefinitions"] = "Role-Assignment",
        ["Microsoft.Authorization/policyAssignments"] = "Azure-Policy",
        ["Microsoft.Authorization/policyDefinitions"] = "Azure-Policy",
        ["Microsoft.Authorization/policySetDefinitions"] = "Azure-Policy",
        ["Microsoft.ManagedIdentity/userAssignedIdentities"] = "Managed-Identities",
        // Security
        ["Microsoft.KeyVault/vaults"] = "Key-Vault",
        ["Microsoft.Security"] = "Defender-for-Cloud",
        ["Microsoft.SecurityInsights"] = "Sentinel",
        ["Microsoft.OperationsManagement/solutions"] = "Sentinel",
        // Compute & containers
        ["Microsoft.Compute/virtualMachines"] = "Virtual-Machine",
        ["Microsoft.Compute/virtualMachineScaleSets"] = "VM-Scale-Sets",
        ["Microsoft.Compute/disks"] = "Managed-Disks",
        ["Microsoft.ContainerService/managedClusters"] = "AKS",
        ["Microsoft.App/containerApps"] = "Container-Apps",
        ["Microsoft.App/managedEnvironments"] = "Container-Apps",
        ["Microsoft.App/jobs"] = "Container-Apps",
        ["Microsoft.ContainerInstance/containerGroups"] = "Container-Instances",
        ["Microsoft.ContainerRegistry/registries"] = "Container-Registry",
        ["Microsoft.Web/serverfarms"] = "App-Service-Plan",
        ["Microsoft.Web/sites"] = "App-Service",
        ["Microsoft.Web/staticSites"] = "Static-Web-Apps",
        ["Microsoft.Batch/batchAccounts"] = "Azure-Batch",
        ["Microsoft.ServiceFabric/clusters"] = "Service-Fabric",
        ["Microsoft.ServiceFabric/managedClusters"] = "Service-Fabric",
        // Storage
        ["Microsoft.Storage/storageAccounts"] = "Storage-Account",
        ["Microsoft.Storage/storageAccounts/blobServices"] = "Blob-Storage",
        ["Microsoft.Storage/storageAccounts/fileServices"] = "Azure-Files",
        ["Microsoft.Storage/storageAccounts/queueServices"] = "Storage-Queue",
        ["Microsoft.Storage/storageAccounts/tableServices"] = "Table-Storage",
        // Databases & analytics
        ["Microsoft.Sql/servers"] = "SQL-Server",
        ["Microsoft.Sql/servers/databases"] = "SQL-Database",
        ["Microsoft.Sql/managedInstances"] = "SQL-Managed-Instance",
        ["Microsoft.DocumentDB/databaseAccounts"] = "Cosmos-DB",
        ["Microsoft.DBforPostgreSQL/flexibleServers"] = "PostgreSQL",
        ["Microsoft.DBforPostgreSQL/servers"] = "PostgreSQL",
        ["Microsoft.DBforMySQL/flexibleServers"] = "MySQL",
        ["Microsoft.DBforMySQL/servers"] = "MySQL",
        ["Microsoft.Cache/redis"] = "Cache-for-Redis",
        ["Microsoft.Cache/redisEnterprise"] = "Cache-for-Redis",
        ["Microsoft.Synapse/workspaces"] = "Synapse-Analytics",
        ["Microsoft.Databricks/workspaces"] = "Databricks",
        ["Microsoft.DataFactory/factories"] = "Data-Factory",
        ["Microsoft.StreamAnalytics/streamingjobs"] = "Stream-Analytics",
        // Integration & messaging
        ["Microsoft.ServiceBus/namespaces"] = "Service-Bus",
        ["Microsoft.EventGrid/topics"] = "Event-Grid",
        ["Microsoft.EventGrid/systemTopics"] = "Event-Grid",
        ["Microsoft.EventGrid/domains"] = "Event-Grid",
        ["Microsoft.EventGrid/namespaces"] = "Event-Grid",
        ["Microsoft.EventHub/namespaces"] = "Event-Hubs",
        ["Microsoft.Logic/workflows"] = "Logic-Apps",
        ["Microsoft.ApiManagement/service"] = "API-Management",
        ["Microsoft.SignalRService/signalR"] = "SignalR",
        ["Microsoft.SignalRService/webPubSub"] = "SignalR",
        ["Microsoft.AppConfiguration/configurationStores"] = "App-Configuration",
        ["Microsoft.NotificationHubs/namespaces"] = "Notification-Hubs",
        // Networking
        ["Microsoft.Network/virtualNetworks"] = "Virtual-Network",
        ["Microsoft.Network/networkSecurityGroups"] = "Network-Security-Group",
        ["Microsoft.Network/publicIPAddresses"] = "Public-IP",
        ["Microsoft.Network/publicIPPrefixes"] = "Public-IP",
        ["Microsoft.Network/networkInterfaces"] = "Network-Interface",
        ["Microsoft.Network/routeTables"] = "Route-Table",
        ["Microsoft.Network/privateEndpoints"] = "Private-Endpoint",
        ["Microsoft.Network/privateLinkServices"] = "Private-Link",
        ["Microsoft.Network/privateDnsZones"] = "Azure-DNS",
        ["Microsoft.Network/dnsZones"] = "Azure-DNS",
        ["Microsoft.Network/loadBalancers"] = "Load-Balancer",
        ["Microsoft.Network/applicationGateways"] = "Application-Gateway",
        ["Microsoft.Network/azureFirewalls"] = "Azure-Firewall",
        ["Microsoft.Network/firewallPolicies"] = "Azure-Firewall",
        ["Microsoft.Network/frontDoors"] = "Front-Door",
        ["Microsoft.Cdn/profiles"] = "Front-Door",
        ["Microsoft.Cdn/profiles/endpoints"] = "Azure-CDN",
        ["Microsoft.Network/expressRouteCircuits"] = "ExpressRoute",
        ["Microsoft.Network/virtualNetworkGateways"] = "VPN-Gateway",
        ["Microsoft.Network/vpnGateways"] = "VPN-Gateway",
        ["Microsoft.Network/natGateways"] = "NAT-Gateway",
        ["Microsoft.Network/bastionHosts"] = "Bastion",
        ["Microsoft.Network/trafficManagerProfiles"] = "Traffic-Manager",
        ["Microsoft.Network/ApplicationGatewayWebApplicationFirewallPolicies"] = "Web-Application-Firewall",
        ["Microsoft.Network/FrontDoorWebApplicationFirewallPolicies"] = "Web-Application-Firewall",
        // AI
        ["Microsoft.CognitiveServices/accounts"] = "AI-Foundry",
        ["Microsoft.CognitiveServices/accounts/deployments"] = "Azure-OpenAI",
        ["Microsoft.MachineLearningServices/workspaces"] = "Machine-Learning",
        ["Microsoft.Search/searchServices"] = "AI-Search",
        ["Microsoft.BotService/botServices"] = "Bot-Services",
        // Observability & operations
        ["Microsoft.Insights/components"] = "Application-Insights",
        ["Microsoft.Insights"] = "Monitor",
        ["Microsoft.OperationalInsights/workspaces"] = "Log-Analytics",
        ["Microsoft.Dashboard/grafana"] = "Managed-Grafana",
        ["Microsoft.Automation/automationAccounts"] = "Automation",
        ["Microsoft.RecoveryServices/vaults"] = "Recovery-Services-Vault",
        ["Microsoft.DataProtection/backupVaults"] = "Recovery-Services-Vault",
        ["Microsoft.Devices/IotHubs"] = "IoT-Hub",
        // Provider defaults (a bare provider namespace matches any type under it)
        ["Microsoft.Network"] = "Network",
        ["Microsoft.Web"] = "App-Service",
        ["Microsoft.Storage"] = "Storage-Account",
        ["Microsoft.Sql"] = "SQL-Database",
        ["Microsoft.DocumentDB"] = "Cosmos-DB",
        ["Microsoft.Compute"] = "Virtual-Machine",
        ["Microsoft.App"] = "Container-Apps",
        ["Microsoft.KeyVault"] = "Key-Vault",
        ["Microsoft.CognitiveServices"] = "AI-Foundry",
        ["Microsoft.Resources"] = "ARM-Template",
        ["Microsoft.Authorization"] = "Security",
    };

    // Every icon id this table can produce; a test asserts they all exist in the UI icon library.
    public static IEnumerable<string> AllIconNames => ByResourceType.Values.Append(Fallback).Distinct();

    // The icon for a resource type such as "Microsoft.Web/sites/slots" (no api version).
    public static string ForResourceType(string resourceType)
    {
        var type = resourceType.Trim();
        while (type.Length > 0)
        {
            if (ByResourceType.TryGetValue(type, out var icon))
                return icon;
            var cut = type.LastIndexOf('/');
            if (cut < 0)
                break;
            type = type[..cut];
        }

        return Fallback;
    }

    // The icon of a deployment's target scope (the model's root node); null keeps the default.
    public static string? ForScope(ResourceScope scope) =>
        scope switch
        {
            ResourceScope.ResourceGroup => "Resource-Group",
            ResourceScope.Subscription => "Subscription",
            ResourceScope.ManagementGroup => "Management-Group",
            _ => null,
        };
}

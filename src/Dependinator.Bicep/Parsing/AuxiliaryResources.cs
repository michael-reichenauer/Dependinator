namespace Dependinator.Bicep.Parsing;

// Resource types that configure another resource rather than being a service of their own: role
// assignments, locks, diagnostic settings, DNS zone groups, app settings, policies. In the
// diagram they are rows under the resource they belong to (NodeType.ResourceMember), like C#
// members, instead of boxes competing with the real services. Only a resource without children
// of its own becomes a row (rows are leaves); anything with an identity or children of its own
// (a private endpoint, a metric alert, a budget) stays a box.
static class AuxiliaryResources
{
    static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.Authorization/roleAssignments",
        "Microsoft.Authorization/locks",
        "Microsoft.Authorization/policyAssignments",
        "Microsoft.Insights/diagnosticSettings",
        "Microsoft.Network/privateEndpoints/privateDnsZoneGroups",
        "Microsoft.Network/privateDnsZones/virtualNetworkLinks",
        "Microsoft.Network/networkSecurityGroups/securityRules",
        "Microsoft.Web/sites/config",
        "Microsoft.Web/sites/slots/config",
        "Microsoft.Sql/servers/auditingSettings",
        "Microsoft.Sql/servers/securityAlertPolicies",
        "Microsoft.Storage/storageAccounts/managementPolicies",
        "Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments",
        "Microsoft.KeyVault/vaults/accessPolicies",
        "Microsoft.ApiManagement/service/policies",
        "Microsoft.ApiManagement/service/apis/policies",
        "Microsoft.ApiManagement/service/products/policies",
        "Microsoft.ApiManagement/service/products/apis",
        "Microsoft.ApiManagement/service/namedValues",
        "Microsoft.ServiceBus/namespaces/topics/subscriptions/rules",
    };

    internal static IEnumerable<string> AllTypes => Types;

    public static bool IsAuxiliary(string resourceType) => Types.Contains(resourceType.Trim());
}

metadata description = 'Key Vault with RBAC authorization, the platform secrets, role assignments and a private endpoint.'

param prefix string
param location string
param tags object

@description('Entra group that administers the vault.')
param adminsGroupId string

@description('Managed identities that may read secrets.')
param secretReaderPrincipalIds string[]

@secure()
param paymentProviderApiKey string

param privateEndpointSubnetId string
param privateDnsZoneId string
param logAnalyticsWorkspaceId string

var roleIds = {
  keyVaultAdministrator: '00482a5a-887f-4fb3-b363-3b7fe8e74483'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-${prefix}'
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enablePurgeProtection: true
    softDeleteRetentionInDays: 90
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
    }
  }
}

resource paymentProviderSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'payment-provider-api-key'
  properties: {
    value: paymentProviderApiKey
    contentType: 'text/plain'
  }
}

resource adminsRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, adminsGroupId, roleIds.keyVaultAdministrator)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.keyVaultAdministrator)
    principalId: adminsGroupId
    principalType: 'Group'
  }
}

resource secretReaderRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in secretReaderPrincipalIds: {
    name: guid(vault.id, principalId, roleIds.keyVaultSecretsUser)
    scope: vault
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.keyVaultSecretsUser)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

resource vaultDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: vault
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        categoryGroup: 'audit'
        enabled: true
      }
    ]
  }
}

module privateEndpoint '../shared/private-endpoint.bicep' = {
  name: 'pe-${vault.name}'
  params: {
    name: 'pe-${prefix}-vault'
    location: location
    tags: tags
    subnetId: privateEndpointSubnetId
    targetResourceId: vault.id
    groupId: 'vault'
    privateDnsZoneId: privateDnsZoneId
  }
}

output vaultName string = vault.name
output vaultUri string = vault.properties.vaultUri
output paymentProviderSecretUri string = paymentProviderSecret.properties.secretUri

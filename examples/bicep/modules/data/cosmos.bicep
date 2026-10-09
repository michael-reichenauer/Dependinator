metadata description = 'Serverless Cosmos DB account holding carts and product-view events, with data-plane RBAC and a private endpoint.'

param prefix string
param location string
param tags object

@description('Identities that read and write documents.')
param dataContributorPrincipalIds string[]

param privateEndpointSubnetId string
param privateDnsZoneId string

// Built-in Cosmos DB data-plane role: Cosmos DB Built-in Data Contributor.
var dataContributorRoleId = '00000000-0000-0000-0000-000000000002'

resource account 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: 'cosmos-${prefix}'
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    backupPolicy: {
      type: 'Continuous'
      continuousModeProperties: {
        tier: 'Continuous7Days'
      }
    }
    disableLocalAuth: true
    publicNetworkAccess: 'Disabled'
    minimalTlsVersion: 'Tls12'
  }

  resource shopDatabase 'sqlDatabases' = {
    name: 'shop'
    properties: {
      resource: {
        id: 'shop'
      }
    }

    resource carts 'containers' = {
      name: 'carts'
      properties: {
        resource: {
          id: 'carts'
          partitionKey: {
            paths: [
              '/customerId'
            ]
            kind: 'Hash'
          }
          defaultTtl: 2592000
        }
      }
    }

    resource productViews 'containers' = {
      name: 'product-views'
      properties: {
        resource: {
          id: 'product-views'
          partitionKey: {
            paths: [
              '/productId'
            ]
            kind: 'Hash'
          }
          defaultTtl: 604800
        }
      }
    }
  }
}

resource dataContributorAssignments 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = [
  for principalId in dataContributorPrincipalIds: {
    parent: account
    name: guid(account.id, principalId, dataContributorRoleId)
    properties: {
      roleDefinitionId: '${account.id}/sqlRoleDefinitions/${dataContributorRoleId}'
      principalId: principalId
      scope: account.id
    }
  }
]

module privateEndpoint '../shared/private-endpoint.bicep' = {
  name: 'pe-${account.name}'
  params: {
    name: 'pe-${prefix}-cosmos'
    location: location
    tags: tags
    subnetId: privateEndpointSubnetId
    targetResourceId: account.id
    groupId: 'Sql'
    privateDnsZoneId: privateDnsZoneId
  }
}

output endpoint string = account.properties.documentEndpoint
output databaseName string = account::shopDatabase.name

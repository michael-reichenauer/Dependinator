metadata description = 'AI Search service that indexes the product catalog, with semantic ranking, RBAC and a private endpoint.'

param prefix string
param location string
param tags object

@description('Identity that writes the catalog index (the apps).')
param indexContributorPrincipalId string

param privateEndpointSubnetId string
param privateDnsZoneId string

var searchIndexDataContributorRoleId = '8ebe5a00-799e-43f5-93ac-243d3dce84a7'

resource search 'Microsoft.Search/searchServices@2023-11-01' = {
  name: 'srch-${prefix}'
  location: location
  tags: tags
  sku: {
    name: 'standard'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'disabled'
    semanticSearch: 'standard'
    authOptions: {
      aadOrApiKey: {
        aadAuthFailureMode: 'http401WithBearerChallenge'
      }
    }
  }
}

resource indexContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, indexContributorPrincipalId, searchIndexDataContributorRoleId)
  scope: search
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataContributorRoleId)
    principalId: indexContributorPrincipalId
    principalType: 'ServicePrincipal'
  }
}

module privateEndpoint '../shared/private-endpoint.bicep' = {
  name: 'pe-${search.name}'
  params: {
    name: 'pe-${prefix}-search'
    location: location
    tags: tags
    subnetId: privateEndpointSubnetId
    targetResourceId: search.id
    groupId: 'searchService'
    privateDnsZoneId: privateDnsZoneId
  }
}

output endpoint string = 'https://${search.name}.search.windows.net'
output principalId string = search.identity.principalId

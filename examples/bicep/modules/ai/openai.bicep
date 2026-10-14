metadata description = 'Azure OpenAI account with the chat and embedding model deployments behind the product assistant, RBAC and a private endpoint.'

param prefix string
param location string
param tags object

@description('Identities that call the models.')
param userPrincipalIds string[]

param privateEndpointSubnetId string
param privateDnsZoneId string

var cognitiveServicesOpenAiUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'

resource account 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: 'oai-${prefix}'
  location: location
  tags: tags
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: 'oai-${prefix}'
    publicNetworkAccess: 'Disabled'
    disableLocalAuth: true
    networkAcls: {
      defaultAction: 'Deny'
    }
  }

  resource chatModel 'deployments' = {
    name: 'gpt-4o-mini'
    sku: {
      name: 'GlobalStandard'
      capacity: 50
    }
    properties: {
      model: {
        format: 'OpenAI'
        name: 'gpt-4o-mini'
        version: '2024-07-18'
      }
      versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
    }
  }

  resource embeddingModel 'deployments' = {
    name: 'text-embedding-3-small'
    sku: {
      name: 'Standard'
      capacity: 120
    }
    properties: {
      model: {
        format: 'OpenAI'
        name: 'text-embedding-3-small'
        version: '1'
      }
    }
    dependsOn: [
      chatModel
    ]
  }
}

resource userRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in userPrincipalIds: {
    name: guid(account.id, principalId, cognitiveServicesOpenAiUserRoleId)
    scope: account
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAiUserRoleId)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

module privateEndpoint '../shared/private-endpoint.bicep' = {
  name: 'pe-${account.name}'
  params: {
    name: 'pe-${prefix}-openai'
    location: location
    tags: tags
    subnetId: privateEndpointSubnetId
    targetResourceId: account.id
    groupId: 'account'
    privateDnsZoneId: privateDnsZoneId
  }
}

output endpoint string = account.properties.endpoint
output chatDeploymentName string = account::chatModel.name
output embeddingDeploymentName string = account::embeddingModel.name

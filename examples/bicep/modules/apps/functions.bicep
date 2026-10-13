metadata description = 'Order processing: an Elastic Premium function app triggered by the Service Bus queues, with its own storage account.'

import { uniqueResourceName } from '../types.bicep'

param prefix string
param location string
param tags object

param jobsIdentityId string
param jobsIdentityPrincipalId string
param jobsIdentityClientId string
param integrationSubnetId string
param appInsightsConnectionString string
param serviceBusNamespace string
param cosmosEndpoint string
param sqlServerFqdn string
param ordersDatabaseName string

var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'

resource functionsStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: uniqueResourceName(prefix, 'func', resourceGroup().id)
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource hostStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(functionsStorage.id, jobsIdentityPrincipalId, storageBlobDataOwnerRoleId)
  scope: functionsStorage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleId)
    principalId: jobsIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-${prefix}-jobs'
  location: location
  tags: tags
  kind: 'elastic'
  sku: {
    name: 'EP1'
    tier: 'ElasticPremium'
  }
  properties: {
    reserved: true
    maximumElasticWorkerCount: 10
  }
}

resource orderProcessor 'Microsoft.Web/sites@2023-12-01' = {
  name: 'func-${prefix}-orders'
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${jobsIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    virtualNetworkSubnetId: integrationSubnetId
    keyVaultReferenceIdentity: jobsIdentityId
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|9.0'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      vnetRouteAllEnabled: true
      functionAppScaleLimit: 10
      minimumElasticInstanceCount: 1
    }
  }
  dependsOn: [
    hostStorageRole
  ]

  resource appSettings 'config' = {
    name: 'appsettings'
    properties: {
      FUNCTIONS_EXTENSION_VERSION: '~4'
      FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
      APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
      AzureWebJobsStorage__accountName: functionsStorage.name
      AzureWebJobsStorage__clientId: jobsIdentityClientId
      ServiceBusConnection__fullyQualifiedNamespace: serviceBusNamespace
      ServiceBusConnection__clientId: jobsIdentityClientId
      Cosmos__Endpoint: cosmosEndpoint
      ConnectionStrings__Orders: 'Server=tcp:${sqlServerFqdn},1433;Database=${ordersDatabaseName};Authentication=Active Directory Managed Identity;User Id=${jobsIdentityClientId};Encrypt=True;'
      AZURE_CLIENT_ID: jobsIdentityClientId
    }
  }
}

output functionAppName string = orderProcessor.name
output functionAppHostName string = orderProcessor.properties.defaultHostName

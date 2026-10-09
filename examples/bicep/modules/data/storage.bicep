metadata description = 'Storage account for product images, invoices, work queues and session tables, with lifecycle rules, RBAC and private endpoints.'

import { uniqueResourceName } from '../types.bicep'

param prefix string
param location string
param tags object

@description('Identity that reads and writes blobs (the web apps).')
param blobContributorPrincipalId string

@description('Identity that processes the work queues (background jobs).')
param queueContributorPrincipalId string

param privateEndpointSubnetId string
param blobPrivateDnsZoneId string
param queuePrivateDnsZoneId string

var roleIds = {
  storageBlobDataContributor: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  storageQueueDataContributor: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: uniqueResourceName(prefix, 'st', resourceGroup().id)
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_ZRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
    }
  }

  resource blobServices 'blobServices' = {
    name: 'default'
    properties: {
      deleteRetentionPolicy: {
        enabled: true
        days: 14
      }
      containerDeleteRetentionPolicy: {
        enabled: true
        days: 14
      }
    }

    resource productImages 'containers' = {
      name: 'product-images'
      properties: {
        publicAccess: 'None'
      }
    }

    resource invoices 'containers' = {
      name: 'invoices'
      properties: {
        publicAccess: 'None'
      }
    }
  }

  resource queueServices 'queueServices' = {
    name: 'default'

    resource thumbnailQueue 'queues' = {
      name: 'thumbnails'
    }

    resource exportQueue 'queues' = {
      name: 'catalog-exports'
    }
  }

  resource tableServices 'tableServices' = {
    name: 'default'

    resource sessions 'tables' = {
      name: 'sessions'
    }
  }
}

resource lifecycle 'Microsoft.Storage/storageAccounts/managementPolicies@2023-05-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    policy: {
      rules: [
        {
          name: 'archive-old-invoices'
          enabled: true
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: [
                'blockBlob'
              ]
              prefixMatch: [
                'invoices/'
              ]
            }
            actions: {
              baseBlob: {
                tierToCool: {
                  daysAfterModificationGreaterThan: 90
                }
                tierToArchive: {
                  daysAfterModificationGreaterThan: 365
                }
              }
            }
          }
        }
      ]
    }
  }
}

resource blobContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, blobContributorPrincipalId, roleIds.storageBlobDataContributor)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageBlobDataContributor)
    principalId: blobContributorPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource queueContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, queueContributorPrincipalId, roleIds.storageQueueDataContributor)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageQueueDataContributor)
    principalId: queueContributorPrincipalId
    principalType: 'ServicePrincipal'
  }
}

module blobPrivateEndpoint '../shared/private-endpoint.bicep' = {
  name: 'pe-${storageAccount.name}-blob'
  params: {
    name: 'pe-${prefix}-blob'
    location: location
    tags: tags
    subnetId: privateEndpointSubnetId
    targetResourceId: storageAccount.id
    groupId: 'blob'
    privateDnsZoneId: blobPrivateDnsZoneId
  }
}

module queuePrivateEndpoint '../shared/private-endpoint.bicep' = {
  name: 'pe-${storageAccount.name}-queue'
  params: {
    name: 'pe-${prefix}-queue'
    location: location
    tags: tags
    subnetId: privateEndpointSubnetId
    targetResourceId: storageAccount.id
    groupId: 'queue'
    privateDnsZoneId: queuePrivateDnsZoneId
  }
}

output storageAccountId string = storageAccount.id
output storageAccountName string = storageAccount.name
output blobEndpoint string = storageAccount.properties.primaryEndpoints.blob
output queueEndpoint string = storageAccount.properties.primaryEndpoints.queue
output productImagesContainerName string = storageAccount::blobServices::productImages.name

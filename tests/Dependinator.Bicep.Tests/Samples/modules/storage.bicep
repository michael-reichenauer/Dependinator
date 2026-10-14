metadata description = 'Storage account with a blob container.'

@description('Azure region.')
param location string

param namePrefix string

param subnetId string

var accountName = toLower(replace('${namePrefix}st', '-', ''))

resource account 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: accountName
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    networkAcls: {
      defaultAction: 'Deny'
      virtualNetworkRules: [{ id: subnetId }]
    }
  }
}

resource blobs 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: account
  name: 'default'
}

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobs
  name: 'uploads'
}

output accountName string = account.name
output blobEndpoint string = account.properties.primaryEndpoints.blob

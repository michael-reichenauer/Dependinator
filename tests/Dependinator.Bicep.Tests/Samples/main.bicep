metadata description = 'Sample web deployment used by the Dependinator.Bicep tests.'

targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Deployment environment name.')
@allowed(['dev', 'prod'])
param environment string

param deployRedis bool = false

param topicNames array = ['orders', 'invoices']

var prefix = 'dep-${environment}'
var tags = { environment: environment }

resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' existing = {
  name: '${prefix}-vnet'
}

@description('The hosting plan shared by the web app and its slot.')
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${prefix}-plan'
  location: location
  tags: tags
  sku: { name: 'B1' }
}

resource web 'Microsoft.Web/sites@2023-12-01' = {
  name: '${prefix}-web'
  location: location
  tags: tags
  properties: {
    serverFarmId: plan.id
    virtualNetworkSubnetId: vnet.properties.subnets[0].id
  }

  resource config 'config' = {
    name: 'appsettings'
    properties: {
      STORAGE_ACCOUNT: storage.outputs.accountName
    }
  }
}

resource slot 'Microsoft.Web/sites/slots@2023-12-01' = {
  parent: web
  name: 'staging'
  location: location
}

resource redis 'Microsoft.Cache/redis@2023-08-01' = if (deployRedis) {
  name: '${prefix}-redis'
  location: location
  properties: {
    sku: { name: 'Basic', family: 'C', capacity: 0 }
  }
  dependsOn: [plan]
}

resource topics 'Microsoft.EventGrid/topics@2022-06-15' = [for name in topicNames: {
  name: '${prefix}-${name}'
  location: location
}]

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    namePrefix: prefix
    subnetId: vnet.properties.subnets[0].id
  }
}

module keyVault 'br/public:avm/res/key-vault/vault:0.9.0' = {
  name: 'keyVault'
  params: {
    name: '${prefix}-kv'
  }
}

output webHost string = web.properties.defaultHostName
output storageAccountName string = storage.outputs.accountName

// Webshop platform: a fictional e-commerce workload on Azure, written to try out and demo
// Dependinator's Bicep support. It compiles with the Bicep compiler, but it is not meant to be
// deployed as-is: names, SKUs, principals and images are illustrative.
metadata description = 'Webshop platform: resource groups, shared services, data stores, messaging, apps, AI and edge for one environment.'

targetScope = 'subscription'

import { environmentName, networkSettings, sqlSettings } from 'modules/types.bicep'

@description('Short name of the workload; prefixes every resource name.')
@minLength(3)
@maxLength(10)
param workload string = 'webshop'

@description('Environment the platform is deployed for.')
param environment environmentName

@description('Azure region for all regional resources.')
param location string = deployment().location

@description('Object id of the Entra group that administers the SQL server and Key Vault.')
param platformAdminsGroupId string

@description('Login name of the SQL administrators group.')
param platformAdminsGroupName string = 'webshop-platform-admins'

@description('Public DNS zone, in the shared DNS resource group, that fronts the shop.')
param dnsZoneName string = 'contoso-webshop.example'

@description('Resource group holding the shared public DNS zone.')
param dnsResourceGroupName string = 'rg-shared-dns'

@description('Container image tags to deploy, per app.')
param imageTags { catalog: string, checkout: string } = { catalog: 'latest', checkout: 'latest' }

@description('Deploy the AI search and OpenAI resources (skipped in dev to save cost).')
param deployAi bool = environment != 'dev'

@description('Email address receiving platform alerts.')
param alertEmail string

@secure()
@description('API key of the payment provider; stored in Key Vault, never in app settings.')
param paymentProviderApiKey string

var prefix = '${workload}-${environment}'
var isProduction = environment == 'prod'
var tags = {
  workload: workload
  environment: environment
  managedBy: 'bicep'
}

var network networkSettings = {
  addressSpace: '10.40.0.0/16'
  subnets: {
    apps: '10.40.1.0/24'
    containerApps: '10.40.2.0/23'
    privateEndpoints: '10.40.4.0/24'
    integration: '10.40.5.0/24'
  }
}

var sql sqlSettings = {
  skuName: isProduction ? 'GP_Gen5_4' : 'GP_S_Gen5_2'
  zoneRedundant: isProduction
}

// ---------------------------------------------------------------------------------------------
// Resource groups: platform (network, identity, monitoring, vault), data stores, apps and edge.
// ---------------------------------------------------------------------------------------------

resource rgPlatform 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${prefix}-platform'
  location: location
  tags: tags
}

resource rgData 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${prefix}-data'
  location: location
  tags: tags
}

resource rgApps 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${prefix}-apps'
  location: location
  tags: tags
}

// A monthly cost budget for the production platform, alerting the platform team at 80 % and 100 %.
resource platformBudget 'Microsoft.Consumption/budgets@2023-11-01' = if (isProduction) {
  name: 'budget-${prefix}-monthly'
  properties: {
    category: 'Cost'
    amount: 4000
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: '2026-11-01'
    }
    filter: {
      tags: {
        name: 'workload'
        operator: 'In'
        values: [
          workload
        ]
      }
    }
    notifications: {
      actualAt80Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [
          alertEmail
        ]
      }
      forecastAt100Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [
          alertEmail
        ]
      }
    }
  }
}

// ---------------------------------------------------------------------------------------------
// Platform services
// ---------------------------------------------------------------------------------------------

module monitoring 'modules/platform/monitoring.bicep' = {
  scope: rgPlatform
  name: 'monitoring'
  params: {
    prefix: prefix
    location: location
    tags: tags
    alertEmail: alertEmail
    retentionInDays: isProduction ? 90 : 30
  }
}

module identity 'modules/platform/identity.bicep' = {
  scope: rgPlatform
  name: 'identity'
  params: {
    prefix: prefix
    location: location
    tags: tags
  }
}

module networking 'modules/platform/network.bicep' = {
  scope: rgPlatform
  name: 'network'
  params: {
    prefix: prefix
    location: location
    tags: tags
    settings: network
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

module keyVault 'modules/security/keyvault.bicep' = {
  scope: rgPlatform
  name: 'key-vault'
  params: {
    prefix: prefix
    location: location
    tags: tags
    adminsGroupId: platformAdminsGroupId
    secretReaderPrincipalIds: [
      identity.outputs.appsIdentityPrincipalId
      identity.outputs.jobsIdentityPrincipalId
    ]
    paymentProviderApiKey: paymentProviderApiKey
    privateEndpointSubnetId: networking.outputs.privateEndpointSubnetId
    privateDnsZoneId: networking.outputs.privateDnsZoneIds.keyVault
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

// ---------------------------------------------------------------------------------------------
// Data stores
// ---------------------------------------------------------------------------------------------

module sqlServer 'modules/data/sql.bicep' = {
  scope: rgData
  name: 'sql'
  params: {
    prefix: prefix
    location: location
    tags: tags
    settings: sql
    lockServer: isProduction
    adminsGroupId: platformAdminsGroupId
    adminsGroupName: platformAdminsGroupName
    privateEndpointSubnetId: networking.outputs.privateEndpointSubnetId
    privateDnsZoneId: networking.outputs.privateDnsZoneIds.sql
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

module storage 'modules/data/storage.bicep' = {
  scope: rgData
  name: 'storage'
  params: {
    prefix: prefix
    location: location
    tags: tags
    blobContributorPrincipalId: identity.outputs.appsIdentityPrincipalId
    queueContributorPrincipalId: identity.outputs.jobsIdentityPrincipalId
    privateEndpointSubnetId: networking.outputs.privateEndpointSubnetId
    blobPrivateDnsZoneId: networking.outputs.privateDnsZoneIds.blob
    queuePrivateDnsZoneId: networking.outputs.privateDnsZoneIds.queue
  }
}

module cosmos 'modules/data/cosmos.bicep' = {
  scope: rgData
  name: 'cosmos'
  params: {
    prefix: prefix
    location: location
    tags: tags
    dataContributorPrincipalIds: [
      identity.outputs.appsIdentityPrincipalId
      identity.outputs.jobsIdentityPrincipalId
    ]
    privateEndpointSubnetId: networking.outputs.privateEndpointSubnetId
    privateDnsZoneId: networking.outputs.privateDnsZoneIds.cosmos
  }
}

module redis 'modules/data/redis.bicep' = {
  scope: rgData
  name: 'redis'
  params: {
    prefix: prefix
    location: location
    tags: tags
    capacity: isProduction ? 2 : 1
    privateEndpointSubnetId: networking.outputs.privateEndpointSubnetId
    privateDnsZoneId: networking.outputs.privateDnsZoneIds.redis
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

// ---------------------------------------------------------------------------------------------
// Messaging
// ---------------------------------------------------------------------------------------------

module serviceBus 'modules/messaging/servicebus.bicep' = {
  scope: rgData
  name: 'service-bus'
  params: {
    prefix: prefix
    location: location
    tags: tags
    senderPrincipalId: identity.outputs.appsIdentityPrincipalId
    receiverPrincipalId: identity.outputs.jobsIdentityPrincipalId
    privateEndpointSubnetId: networking.outputs.privateEndpointSubnetId
    privateDnsZoneId: networking.outputs.privateDnsZoneIds.serviceBus
  }
}

module events 'modules/messaging/events.bicep' = {
  scope: rgData
  name: 'events'
  params: {
    prefix: prefix
    location: location
    tags: tags
    storageAccountId: storage.outputs.storageAccountId
    thumbnailQueueId: serviceBus.outputs.thumbnailQueueId
  }
}

// ---------------------------------------------------------------------------------------------
// Apps
// ---------------------------------------------------------------------------------------------

module appService 'modules/apps/app-service.bicep' = {
  scope: rgApps
  name: 'app-service'
  params: {
    prefix: prefix
    location: location
    tags: tags
    zoneRedundant: isProduction
    appsIdentityId: identity.outputs.appsIdentityId
    appsIdentityClientId: identity.outputs.appsIdentityClientId
    appsSubnetId: networking.outputs.appsSubnetId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    keyVaultName: keyVault.outputs.vaultName
    sqlServerFqdn: sqlServer.outputs.serverFqdn
    catalogDatabaseName: sqlServer.outputs.catalogDatabaseName
    redisHostName: redis.outputs.hostName
    serviceBusNamespace: serviceBus.outputs.namespaceFqdn
    cosmosEndpoint: cosmos.outputs.endpoint
    blobEndpoint: storage.outputs.blobEndpoint
    searchEndpoint: deployAi ? search!.outputs.endpoint : ''
  }
}

module functions 'modules/apps/functions.bicep' = {
  scope: rgApps
  name: 'functions'
  params: {
    prefix: prefix
    location: location
    tags: tags
    jobsIdentityId: identity.outputs.jobsIdentityId
    jobsIdentityPrincipalId: identity.outputs.jobsIdentityPrincipalId
    jobsIdentityClientId: identity.outputs.jobsIdentityClientId
    integrationSubnetId: networking.outputs.integrationSubnetId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    serviceBusNamespace: serviceBus.outputs.namespaceFqdn
    cosmosEndpoint: cosmos.outputs.endpoint
    sqlServerFqdn: sqlServer.outputs.serverFqdn
    ordersDatabaseName: sqlServer.outputs.ordersDatabaseName
  }
}

module containerApps 'modules/apps/container-apps.bicep' = {
  scope: rgApps
  name: 'container-apps'
  params: {
    prefix: prefix
    location: location
    tags: tags
    zoneRedundant: isProduction
    imageTags: imageTags
    appsIdentityId: identity.outputs.appsIdentityId
    appsIdentityPrincipalId: identity.outputs.appsIdentityPrincipalId
    appsIdentityClientId: identity.outputs.appsIdentityClientId
    containerAppsSubnetId: networking.outputs.containerAppsSubnetId
    privateEndpointSubnetId: networking.outputs.privateEndpointSubnetId
    registryPrivateDnsZoneId: networking.outputs.privateDnsZoneIds.registry
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    sqlServerFqdn: sqlServer.outputs.serverFqdn
    catalogDatabaseName: sqlServer.outputs.catalogDatabaseName
    cosmosEndpoint: cosmos.outputs.endpoint
    serviceBusNamespace: serviceBus.outputs.namespaceFqdn
  }
}

module apiManagement 'modules/apps/api-management.bicep' = {
  scope: rgApps
  name: 'api-management'
  params: {
    prefix: prefix
    location: location
    tags: tags
    skuName: isProduction ? 'StandardV2' : 'Developer'
    appsIdentityId: identity.outputs.appsIdentityId
    appsIdentityClientId: identity.outputs.appsIdentityClientId
    keyVaultUri: keyVault.outputs.vaultUri
    catalogApiUrl: 'https://${appService.outputs.apiHostName}'
    checkoutApiUrl: 'https://${containerApps.outputs.checkoutFqdn}'
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

module frontDoor 'modules/apps/front-door.bicep' = {
  scope: rgApps
  name: 'front-door'
  params: {
    prefix: prefix
    tags: tags
    customDomainName: 'shop.${dnsZoneName}'
    webOriginHostName: appService.outputs.frontendHostName
    apiOriginHostName: apiManagement.outputs.gatewayHostName
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

module dns 'modules/platform/dns.bicep' = {
  scope: resourceGroup(dnsResourceGroupName)
  name: 'dns'
  params: {
    zoneName: dnsZoneName
    recordName: 'shop'
    frontDoorEndpointHostName: frontDoor.outputs.endpointHostName
    domainValidationToken: frontDoor.outputs.customDomainValidationToken
  }
}

// ---------------------------------------------------------------------------------------------
// AI (optional)
// ---------------------------------------------------------------------------------------------

module search 'modules/ai/search.bicep' = if (deployAi) {
  scope: rgApps
  name: 'search'
  params: {
    prefix: prefix
    location: location
    tags: tags
    indexContributorPrincipalId: identity.outputs.appsIdentityPrincipalId
    privateEndpointSubnetId: networking.outputs.privateEndpointSubnetId
    privateDnsZoneId: networking.outputs.privateDnsZoneIds.search
  }
}

module openAi 'modules/ai/openai.bicep' = if (deployAi) {
  scope: rgApps
  name: 'openai'
  params: {
    prefix: prefix
    location: location
    tags: tags
    userPrincipalIds: [
      identity.outputs.appsIdentityPrincipalId
      identity.outputs.jobsIdentityPrincipalId
    ]
    privateEndpointSubnetId: networking.outputs.privateEndpointSubnetId
    privateDnsZoneId: networking.outputs.privateDnsZoneIds.openAi
  }
}

// ---------------------------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------------------------

@description('Public entry point of the shop (the Front Door endpoint).')
output shopUrl string = 'https://${frontDoor.outputs.endpointHostName}'
output apiGatewayUrl string = apiManagement.outputs.gatewayUrl
output frontendHostName string = appService.outputs.frontendHostName
output catalogAppFqdn string = containerApps.outputs.catalogFqdn
output keyVaultName string = keyVault.outputs.vaultName
output logAnalyticsWorkspaceId string = monitoring.outputs.logAnalyticsWorkspaceId
output resourceGroupNames object = {
  platform: rgPlatform.name
  data: rgData.name
  apps: rgApps.name
}

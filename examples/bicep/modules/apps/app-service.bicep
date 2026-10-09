metadata description = 'Linux App Service plan with the storefront and the catalog API web apps, a staging slot, Key Vault referenced settings and diagnostics.'

param prefix string
param location string
param tags object
param zoneRedundant bool

param appsIdentityId string
param appsIdentityClientId string
param appsSubnetId string
param appInsightsConnectionString string
param logAnalyticsWorkspaceId string
param keyVaultName string
param sqlServerFqdn string
param catalogDatabaseName string
param redisHostName string
param serviceBusNamespace string
param cosmosEndpoint string
param blobEndpoint string

@description('Endpoint of the AI search service, or empty when AI is not deployed.')
param searchEndpoint string = ''

var commonSettings = {
  APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
  ApplicationInsightsAgent_EXTENSION_VERSION: '~3'
  AZURE_CLIENT_ID: appsIdentityClientId
  WEBSITE_RUN_FROM_PACKAGE: '1'
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-${prefix}'
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: zoneRedundant ? 'P1v3' : 'B1'
    tier: zoneRedundant ? 'PremiumV3' : 'Basic'
    capacity: zoneRedundant ? 3 : 1
  }
  properties: {
    reserved: true
    zoneRedundant: zoneRedundant
  }
}

resource frontend 'Microsoft.Web/sites@2023-12-01' = {
  name: 'app-${prefix}-web'
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${appsIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    virtualNetworkSubnetId: appsSubnetId
    keyVaultReferenceIdentity: appsIdentityId
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: true
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: '/health'
      vnetRouteAllEnabled: true
      ipSecurityRestrictionsDefaultAction: 'Deny'
      ipSecurityRestrictions: [
        {
          name: 'allow-front-door'
          priority: 100
          action: 'Allow'
          tag: 'ServiceTag'
          ipAddress: 'AzureFrontDoor.Backend'
        }
      ]
    }
  }

  resource appSettings 'config' = {
    name: 'appsettings'
    properties: union(commonSettings, {
      Catalog__ApiBaseUrl: 'https://${api.properties.defaultHostName}'
      Redis__HostName: redisHostName
      Storage__BlobEndpoint: blobEndpoint
      Search__Endpoint: searchEndpoint
    })
  }
}

resource api 'Microsoft.Web/sites@2023-12-01' = {
  name: 'app-${prefix}-api'
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${appsIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    virtualNetworkSubnetId: appsSubnetId
    keyVaultReferenceIdentity: appsIdentityId
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: true
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: '/health'
      vnetRouteAllEnabled: true
    }
  }

  resource appSettings 'config' = {
    name: 'appsettings'
    properties: union(commonSettings, {
      ConnectionStrings__Catalog: 'Server=tcp:${sqlServerFqdn},1433;Database=${catalogDatabaseName};Authentication=Active Directory Managed Identity;User Id=${appsIdentityClientId};Encrypt=True;'
      Cosmos__Endpoint: cosmosEndpoint
      ServiceBus__FullyQualifiedNamespace: serviceBusNamespace
      Redis__HostName: redisHostName
      Payments__ApiKey: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=payment-provider-api-key)'
    })
  }

  resource staging 'slots' = {
    name: 'staging'
    location: location
    tags: tags
    kind: 'app,linux'
    identity: {
      type: 'UserAssigned'
      userAssignedIdentities: {
        '${appsIdentityId}': {}
      }
    }
    properties: {
      serverFarmId: plan.id
      httpsOnly: true
      virtualNetworkSubnetId: appsSubnetId
      siteConfig: {
        linuxFxVersion: 'DOTNETCORE|9.0'
        minTlsVersion: '1.2'
        ftpsState: 'Disabled'
        healthCheckPath: '/health'
        autoSwapSlotName: 'production'
      }
    }
  }
}

resource apiDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: api
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

output planId string = plan.id
output frontendHostName string = frontend.properties.defaultHostName
output apiHostName string = api.properties.defaultHostName
output apiStagingHostName string = api::staging.properties.defaultHostName

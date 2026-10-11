metadata description = 'API Management (Standard v2, VNet integrated) publishing the catalog and checkout APIs over the private network, with the payment key as a Key Vault named value.'

param prefix string
param location string
param tags object

@description('A v2 tier: only Standard v2 and Premium v2 support outbound VNet integration.')
@allowed([
  'StandardV2'
  'PremiumV2'
])
param skuName string = 'StandardV2'

@description('Delegated subnet (Microsoft.Web/serverFarms) the gateway uses to reach the backends.')
param apimSubnetId string

param appsIdentityId string
param appsIdentityClientId string
param keyVaultUri string
param catalogApiUrl string
param checkoutApiUrl string
param logAnalyticsWorkspaceId string

resource apim 'Microsoft.ApiManagement/service@2023-05-01-preview' = {
  name: 'apim-${prefix}'
  location: location
  tags: tags
  sku: {
    name: skuName
    capacity: 1
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${appsIdentityId}': {}
    }
  }
  properties: {
    publisherEmail: 'api-platform@contoso-webshop.example'
    publisherName: 'Contoso Webshop'
    // Outbound VNet integration: the gateway stays public (Front Door calls it) but reaches the
    // catalog API's private endpoint and the internal Container Apps environment over the VNet.
    publicNetworkAccess: 'Enabled'
    virtualNetworkType: 'External'
    virtualNetworkConfiguration: {
      subnetResourceId: apimSubnetId
    }
    customProperties: {
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls10': 'false'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls11': 'false'
    }
  }

  resource paymentApiKey 'namedValues' = {
    name: 'payment-provider-api-key'
    properties: {
      displayName: 'payment-provider-api-key'
      secret: true
      keyVault: {
        secretIdentifier: '${keyVaultUri}secrets/payment-provider-api-key'
        identityClientId: appsIdentityClientId
      }
    }
  }

  resource catalogApi 'apis' = {
    name: 'catalog'
    properties: {
      displayName: 'Catalog API'
      description: 'Products, categories and search.'
      path: 'catalog'
      protocols: [
        'https'
      ]
      serviceUrl: catalogApiUrl
      subscriptionRequired: true
      apiRevision: '1'
    }

    resource rateLimitPolicy 'policies' = {
      name: 'policy'
      properties: {
        format: 'rawxml'
        value: '<policies><inbound><base /><rate-limit calls="300" renewal-period="60" /></inbound><backend><base /></backend><outbound><base /></outbound><on-error><base /></on-error></policies>'
      }
    }
  }

  resource checkoutApi 'apis' = {
    name: 'checkout'
    properties: {
      displayName: 'Checkout API'
      description: 'Carts, orders and payments.'
      path: 'checkout'
      protocols: [
        'https'
      ]
      serviceUrl: checkoutApiUrl
      subscriptionRequired: true
      apiRevision: '1'
    }
  }

  resource storefrontProduct 'products' = {
    name: 'storefront'
    properties: {
      displayName: 'Storefront'
      description: 'APIs used by the web storefront and the mobile apps.'
      subscriptionRequired: true
      approvalRequired: false
      state: 'published'
    }

    resource catalogLink 'apis' = {
      name: catalogApi.name
    }

    resource checkoutLink 'apis' = {
      name: checkoutApi.name
    }
  }
}

resource apimDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: apim
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'GatewayLogs'
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

output serviceName string = apim.name
output gatewayUrl string = apim.properties.gatewayUrl
output gatewayHostName string = replace(apim.properties.gatewayUrl, 'https://', '')

metadata description = 'Virtual network with delegated subnets, NSGs, outbound NAT and the private DNS zones of every private endpoint.'

import { networkSettings } from '../types.bicep'

param prefix string
param location string
param tags object
param settings networkSettings
param logAnalyticsWorkspaceId string

// One private DNS zone per private-link service used by the platform, keyed by the name the
// other modules use to look up the zone id.
var privateDnsZoneNames = {
  keyVault: 'privatelink.vaultcore.azure.net'
  sql: 'privatelink${environment().suffixes.sqlServerHostname}'
  blob: 'privatelink.blob.${environment().suffixes.storage}'
  queue: 'privatelink.queue.${environment().suffixes.storage}'
  table: 'privatelink.table.${environment().suffixes.storage}'
  cosmos: 'privatelink.documents.azure.com'
  redis: 'privatelink.redis.cache.windows.net'
  serviceBus: 'privatelink.servicebus.windows.net'
  search: 'privatelink.search.windows.net'
  openAi: 'privatelink.openai.azure.com'
  registry: 'privatelink.azurecr.io'
  sites: 'privatelink.azurewebsites.net'
}

resource appsNsg 'Microsoft.Network/networkSecurityGroups@2024-01-01' = {
  name: 'nsg-${prefix}-apps'
  location: location
  tags: tags
  properties: {
    // The web apps have no public endpoints (Front Door uses Private Link, API Management the
    // API's private endpoint), so nothing from the Internet is expected on this subnet.
    securityRules: [
      {
        name: 'deny-internet-inbound'
        properties: {
          priority: 4000
          direction: 'Inbound'
          access: 'Deny'
          protocol: '*'
          sourceAddressPrefix: 'Internet'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
        }
      }
    ]
  }
}

resource privateEndpointsNsg 'Microsoft.Network/networkSecurityGroups@2024-01-01' = {
  name: 'nsg-${prefix}-private-endpoints'
  location: location
  tags: tags
  properties: {
    securityRules: [
      {
        name: 'allow-vnet-inbound'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: '*'
          sourceAddressPrefix: 'VirtualNetwork'
          sourcePortRange: '*'
          destinationAddressPrefix: settings.subnets.privateEndpoints
          destinationPortRange: '*'
        }
      }
    ]
  }
}

resource natPublicIp 'Microsoft.Network/publicIPAddresses@2024-01-01' = {
  name: 'pip-${prefix}-nat'
  location: location
  tags: tags
  sku: {
    name: 'Standard'
  }
  zones: [
    '1'
    '2'
    '3'
  ]
  properties: {
    publicIPAllocationMethod: 'Static'
    publicIPAddressVersion: 'IPv4'
  }
}

resource natGateway 'Microsoft.Network/natGateways@2024-01-01' = {
  name: 'ng-${prefix}'
  location: location
  tags: tags
  sku: {
    name: 'Standard'
  }
  properties: {
    idleTimeoutInMinutes: 10
    publicIpAddresses: [
      {
        id: natPublicIp.id
      }
    ]
  }
}

resource vnet 'Microsoft.Network/virtualNetworks@2024-01-01' = {
  name: 'vnet-${prefix}'
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        settings.addressSpace
      ]
    }
    subnets: [
      {
        name: 'snet-apps'
        properties: {
          addressPrefix: settings.subnets.apps
          networkSecurityGroup: {
            id: appsNsg.id
          }
          natGateway: {
            id: natGateway.id
          }
          delegations: [
            {
              name: 'app-service'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
      {
        name: 'snet-container-apps'
        properties: {
          addressPrefix: settings.subnets.containerApps
          natGateway: {
            id: natGateway.id
          }
          delegations: [
            {
              name: 'container-apps'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        name: 'snet-private-endpoints'
        properties: {
          addressPrefix: settings.subnets.privateEndpoints
          networkSecurityGroup: {
            id: privateEndpointsNsg.id
          }
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
      {
        name: 'snet-integration'
        properties: {
          addressPrefix: settings.subnets.integration
          natGateway: {
            id: natGateway.id
          }
          delegations: [
            {
              name: 'functions'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
      {
        name: 'snet-apim'
        properties: {
          addressPrefix: settings.subnets.apim
          natGateway: {
            id: natGateway.id
          }
          delegations: [
            {
              name: 'api-management'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
    ]
  }
}

resource vnetDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: vnet
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        categoryGroup: 'allLogs'
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

resource privateDnsZones 'Microsoft.Network/privateDnsZones@2024-06-01' = [
  for zone in items(privateDnsZoneNames): {
    name: zone.value
    location: 'global'
    tags: tags
  }
]

resource privateDnsZoneLinks 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = [
  for (zone, i) in items(privateDnsZoneNames): {
    parent: privateDnsZones[i]
    name: 'link-${vnet.name}'
    location: 'global'
    tags: tags
    properties: {
      virtualNetwork: {
        id: vnet.id
      }
      registrationEnabled: false
    }
  }
]

output vnetId string = vnet.id
output appsSubnetId string = resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'snet-apps')
output containerAppsSubnetId string = resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'snet-container-apps')
output privateEndpointSubnetId string = resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'snet-private-endpoints')
output integrationSubnetId string = resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'snet-integration')
output apimSubnetId string = resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'snet-apim')

@description('Private DNS zone resource ids by service key (keyVault, sql, blob, queue, table, cosmos, redis, serviceBus, search, openAi, registry, sites).')
output privateDnsZoneIds object = toObject(
  items(privateDnsZoneNames),
  entry => entry.key,
  entry => resourceId('Microsoft.Network/privateDnsZones', entry.value)
)

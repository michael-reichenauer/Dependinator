metadata description = 'Azure Cache for Redis for sessions and the catalog cache, reachable only through a private endpoint.'

param prefix string
param location string
param tags object

@minValue(0)
@maxValue(6)
param capacity int = 1

param privateEndpointSubnetId string
param privateDnsZoneId string
param logAnalyticsWorkspaceId string

resource cache 'Microsoft.Cache/redis@2024-03-01' = {
  name: 'redis-${prefix}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'Standard'
      family: 'C'
      capacity: capacity
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'
    redisConfiguration: {
      'maxmemory-policy': 'allkeys-lru'
    }
  }
}

resource cacheDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: cache
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

module privateEndpoint '../shared/private-endpoint.bicep' = {
  name: 'pe-${cache.name}'
  params: {
    name: 'pe-${prefix}-redis'
    location: location
    tags: tags
    subnetId: privateEndpointSubnetId
    targetResourceId: cache.id
    groupId: 'redisCache'
    privateDnsZoneId: privateDnsZoneId
  }
}

output hostName string = cache.properties.hostName
output sslPort int = cache.properties.sslPort

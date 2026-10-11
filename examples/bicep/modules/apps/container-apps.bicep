metadata description = 'Container registry and an internal Container Apps environment (reachable from the VNet only) running the catalog and checkout services.'

import { uniqueResourceName } from '../types.bicep'

param prefix string
param location string
param tags object
param zoneRedundant bool
param imageTags { catalog: string, checkout: string }

param appsIdentityId string
param appsIdentityPrincipalId string
param appsIdentityClientId string
param vnetId string
param containerAppsSubnetId string
param privateEndpointSubnetId string
param registryPrivateDnsZoneId string
param logAnalyticsWorkspaceId string
param appInsightsConnectionString string
param sqlServerFqdn string
param catalogDatabaseName string
param cosmosEndpoint string
param serviceBusNamespace string

var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: uniqueResourceName(prefix, 'cr', resourceGroup().id)
  location: location
  tags: tags
  sku: {
    name: 'Premium'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Disabled'
    zoneRedundancy: zoneRedundant ? 'Enabled' : 'Disabled'
    policies: {
      retentionPolicy: {
        status: 'enabled'
        days: 30
      }
    }
  }
}

resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, appsIdentityPrincipalId, acrPullRoleId)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: appsIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

module registryPrivateEndpoint '../shared/private-endpoint.bicep' = {
  name: 'pe-${registry.name}'
  params: {
    name: 'pe-${prefix}-registry'
    location: location
    tags: tags
    subnetId: privateEndpointSubnetId
    targetResourceId: registry.id
    groupId: 'registry'
    privateDnsZoneId: registryPrivateDnsZoneId
  }
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: last(split(logAnalyticsWorkspaceId, '/'))
  scope: resourceGroup(split(logAnalyticsWorkspaceId, '/')[4])
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${prefix}'
  location: location
  tags: tags
  properties: {
    zoneRedundant: zoneRedundant
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    vnetConfiguration: {
      infrastructureSubnetId: containerAppsSubnetId
      // Internal load balancer: the apps are reachable from the VNet (API Management) only.
      internal: true
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

// An internal environment needs a private DNS zone for its default domain, so VNet clients
// (API Management, the web apps) resolve the apps' FQDNs to the environment's internal IP. The
// domain is only known once the environment exists, so the zone is created by a nested module.
module environmentDns 'container-apps-dns.bicep' = {
  name: 'cae-${prefix}-dns'
  params: {
    zoneName: environment.properties.defaultDomain
    staticIp: environment.properties.staticIp
    vnetId: vnetId
    tags: tags
  }
}

var commonEnv = [
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: appInsightsConnectionString
  }
  {
    name: 'AZURE_CLIENT_ID'
    value: appsIdentityClientId
  }
  {
    name: 'ServiceBus__FullyQualifiedNamespace'
    value: serviceBusNamespace
  }
]

resource catalog 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-${prefix}-catalog'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${appsIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: appsIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'catalog'
          image: '${registry.properties.loginServer}/webshop/catalog:${imageTags.catalog}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: concat(commonEnv, [
            {
              name: 'ConnectionStrings__Catalog'
              value: 'Server=tcp:${sqlServerFqdn},1433;Database=${catalogDatabaseName};Authentication=Active Directory Managed Identity;User Id=${appsIdentityClientId};Encrypt=True;'
            }
          ])
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
              }
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
              }
            }
          ]
        }
      ]
      scale: {
        minReplicas: zoneRedundant ? 2 : 0
        maxReplicas: 10
        rules: [
          {
            name: 'http-concurrency'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
  dependsOn: [
    acrPullRole
  ]
}

resource checkout 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-${prefix}-checkout'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${appsIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: appsIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'checkout'
          image: '${registry.properties.loginServer}/webshop/checkout:${imageTags.checkout}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: concat(commonEnv, [
            {
              name: 'Cosmos__Endpoint'
              value: cosmosEndpoint
            }
            {
              name: 'Catalog__BaseUrl'
              value: 'https://${catalog.properties.configuration.ingress.fqdn}'
            }
          ])
        }
      ]
      scale: {
        minReplicas: zoneRedundant ? 2 : 0
        maxReplicas: 20
        rules: [
          {
            name: 'http-concurrency'
            http: {
              metadata: {
                concurrentRequests: '30'
              }
            }
          }
        ]
      }
    }
  }
  dependsOn: [
    acrPullRole
  ]
}

output registryLoginServer string = registry.properties.loginServer
output environmentId string = environment.id
output catalogFqdn string = catalog.properties.configuration.ingress.fqdn
output checkoutFqdn string = checkout.properties.configuration.ingress.fqdn

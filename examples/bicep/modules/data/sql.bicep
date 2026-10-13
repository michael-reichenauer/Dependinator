metadata description = 'Azure SQL logical server with Entra-only authentication, the catalog and orders databases, auditing and a private endpoint.'

import { sqlSettings } from '../types.bicep'

param prefix string
param location string
param tags object
param settings sqlSettings

@description('Protects the server (and its databases) from deletion; on in production.')
param lockServer bool = false

@description('Entra group that is the server administrator.')
param adminsGroupId string
param adminsGroupName string

param privateEndpointSubnetId string
param privateDnsZoneId string
param logAnalyticsWorkspaceId string

resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: 'sql-${prefix}'
  location: location
  tags: tags
  properties: {
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'
    restrictOutboundNetworkAccess: 'Disabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      principalType: 'Group'
      login: adminsGroupName
      sid: adminsGroupId
      tenantId: subscription().tenantId
    }
  }
}

resource catalogDatabase 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: 'catalog'
  location: location
  tags: tags
  sku: {
    name: settings.skuName
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: settings.zoneRedundant
    requestedBackupStorageRedundancy: settings.zoneRedundant ? 'Zone' : 'Local'
  }
}

resource ordersDatabase 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: 'orders'
  location: location
  tags: tags
  sku: {
    name: settings.skuName
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: settings.zoneRedundant
    requestedBackupStorageRedundancy: settings.zoneRedundant ? 'Zone' : 'Local'
  }
}

resource deleteLock 'Microsoft.Authorization/locks@2020-05-01' = if (lockServer) {
  name: 'do-not-delete'
  scope: sqlServer
  properties: {
    level: 'CanNotDelete'
    notes: 'Production databases; remove the lock before deleting the server.'
  }
}

resource auditing 'Microsoft.Sql/servers/auditingSettings@2021-11-01' = {
  parent: sqlServer
  name: 'default'
  properties: {
    state: 'Enabled'
    isAzureMonitorTargetEnabled: true
  }
}

resource masterDatabase 'Microsoft.Sql/servers/databases@2021-11-01' existing = {
  parent: sqlServer
  name: 'master'
}

resource auditDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'audit-to-log-analytics'
  scope: masterDatabase
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'SQLSecurityAuditEvents'
        enabled: true
      }
    ]
  }
}

module privateEndpoint '../shared/private-endpoint.bicep' = {
  name: 'pe-${sqlServer.name}'
  params: {
    name: 'pe-${prefix}-sql'
    location: location
    tags: tags
    subnetId: privateEndpointSubnetId
    targetResourceId: sqlServer.id
    groupId: 'sqlServer'
    privateDnsZoneId: privateDnsZoneId
  }
}

output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output catalogDatabaseName string = catalogDatabase.name
output ordersDatabaseName string = ordersDatabase.name

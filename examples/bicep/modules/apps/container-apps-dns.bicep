metadata description = 'Private DNS zone for an internal Container Apps environment: every app FQDN under the default domain resolves to the environment load balancer.'

@description('Default domain of the environment, e.g. happyforest-12ab34cd.swedencentral.azurecontainerapps.io.')
param zoneName string

@description('Internal static IP of the environment load balancer.')
param staticIp string

param vnetId string
param tags object

resource zone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: zoneName
  location: 'global'
  tags: tags

  resource vnetLink 'virtualNetworkLinks' = {
    name: 'link-container-apps'
    location: 'global'
    properties: {
      virtualNetwork: {
        id: vnetId
      }
      registrationEnabled: false
    }
  }

  resource wildcard 'A' = {
    name: '*'
    properties: {
      ttl: 300
      aRecords: [
        {
          ipv4Address: staticIp
        }
      ]
    }
  }
}

output zoneId string = zone.id

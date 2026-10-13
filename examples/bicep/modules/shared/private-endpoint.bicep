metadata description = 'A private endpoint for one sub-resource of a service, registered in its private DNS zone.'

@description('Name of the private endpoint, e.g. pe-webshop-dev-sql.')
param name string
param location string
param tags object

@description('Subnet the endpoint NIC is placed in.')
param subnetId string

@description('Resource id of the service the endpoint connects to.')
param targetResourceId string

@description('Private-link sub-resource (group id), e.g. sqlServer, blob, vault.')
param groupId string

@description('Private DNS zone that resolves the service host name to the endpoint IP.')
param privateDnsZoneId string

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2024-01-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    subnet: {
      id: subnetId
    }
    customNetworkInterfaceName: '${name}-nic'
    privateLinkServiceConnections: [
      {
        name: name
        properties: {
          privateLinkServiceId: targetResourceId
          groupIds: [
            groupId
          ]
        }
      }
    ]
  }

  resource dnsZoneGroup 'privateDnsZoneGroups' = {
    name: 'default'
    properties: {
      privateDnsZoneConfigs: [
        {
          name: groupId
          properties: {
            privateDnsZoneId: privateDnsZoneId
          }
        }
      ]
    }
  }
}

output privateEndpointId string = privateEndpoint.id
output networkInterfaceId string = privateEndpoint.properties.networkInterfaces[0].id

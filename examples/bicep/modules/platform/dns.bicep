metadata description = 'Public DNS records that point the shop host name at Front Door; the zone itself is shared and already exists.'

@description('Name of the existing public DNS zone, e.g. contoso-webshop.example.')
param zoneName string

@description('Record name below the zone, e.g. shop for shop.contoso-webshop.example.')
param recordName string

param frontDoorEndpointHostName string

@description('Token Front Door issued for validating ownership of the custom domain.')
param domainValidationToken string

resource zone 'Microsoft.Network/dnsZones@2018-05-01' existing = {
  name: zoneName
}

resource shopRecord 'Microsoft.Network/dnsZones/CNAME@2018-05-01' = {
  parent: zone
  name: recordName
  properties: {
    TTL: 300
    CNAMERecord: {
      cname: frontDoorEndpointHostName
    }
  }
}

resource domainValidationRecord 'Microsoft.Network/dnsZones/TXT@2018-05-01' = {
  parent: zone
  name: '_dnsauth.${recordName}'
  properties: {
    TTL: 3600
    TXTRecords: [
      {
        value: [
          domainValidationToken
        ]
      }
    ]
  }
}

output shopHostName string = '${recordName}.${zoneName}'

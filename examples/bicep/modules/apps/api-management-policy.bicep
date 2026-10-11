metadata description = 'Global API Management policy applied after Front Door exists: only requests carrying this Front Door profile id (X-Azure-FDID) reach the APIs.'

param apimName string

@description('Front Door profile id, sent by Front Door in the X-Azure-FDID header.')
param frontDoorId string

resource apim 'Microsoft.ApiManagement/service@2023-05-01-preview' existing = {
  name: apimName
}

resource globalPolicy 'Microsoft.ApiManagement/service/policies@2023-05-01-preview' = {
  parent: apim
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: '<policies><inbound><check-header name="X-Azure-FDID" failed-check-httpcode="403" failed-check-error-message="Access is allowed through Front Door only" ignore-case="true"><value>${frontDoorId}</value></check-header><base /></inbound><backend><base /></backend><outbound><base /></outbound><on-error><base /></on-error></policies>'
  }
}
